using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.Review;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Listeners;

public sealed class SolutionEventsHandler : IVsSolutionEvents, IVsRunningDocTableEvents, IDisposable
{
    private uint _solutionCookie;
    private IVsSolution _solution;

    private uint _rdtCookie;
    private IVsRunningDocumentTable _rdt;

    private readonly object _sync = new();
    private readonly Dictionary<string, HeadWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public async Task Initialize(IServiceProvider sp)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        _solution = (IVsSolution)sp.GetService(typeof(SVsSolution));
        _solution?.AdviseSolutionEvents(this, out _solutionCookie);

        _rdt = (IVsRunningDocumentTable)sp.GetService(typeof(SVsRunningDocumentTable));
        _rdt?.AdviseRunningDocTableEvents(this, out _rdtCookie);

        await RebuildRepoWatchersAsync();
    }


    private async Task RebuildRepoWatchersAsync()
    {
        await TaskScheduler.Default;

        var currentRepos = GetRepoRootsFromOpenDocs();

        lock (_sync)
        {
            foreach (var stale in _watchers.Keys.Except(currentRepos, StringComparer.OrdinalIgnoreCase).ToList())
            {
                _watchers[stale].Dispose();
                _watchers.Remove(stale);
            }

            foreach (var root in currentRepos)
            {
                if (!_watchers.TryGetValue(root, out var hw))
                {
                    hw = new HeadWatcher(root, OnBranchChangedAsync);
                    _watchers[root] = hw;
                }
                hw.EnsureWatching();
            }
        }
    }

    private IEnumerable<string> GetRepoRootsFromOpenDocs()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_rdt == null) return roots;

        try
        {
            var hr = _rdt.GetRunningDocumentsEnum(out var en);
            if (ErrorHandler.Failed(hr) || en == null) return roots;

            uint fetched;
            var cookies = new uint[1];

            while (en.Next(1, cookies, out fetched) == VSConstants.S_OK && fetched == 1)
            {
                string moniker = null;
                IntPtr punk = IntPtr.Zero;

                try
                {
                    _rdt.GetDocumentInfo(
                        cookies[0],
                        out _,            // flags
                        out _,            // readLocks
                        out _,            // editLocks
                        out moniker,      // pbstrMkDocument
                        out _,            // hierarchy
                        out _,            // itemid
                        out punk          // ppunkDocData (AddRef'ed by RDT)
                    );

                    if (string.IsNullOrWhiteSpace(moniker)) continue;
                    if (!File.Exists(moniker)) continue;

                    var repo = GitRepoHelper.FindRepoRootForFile(moniker);
                    if (!string.IsNullOrEmpty(repo))
                        roots.Add(repo);
                }
                finally
                {
                    if (punk != IntPtr.Zero)
                    {
                        try { Marshal.Release(punk); } catch { /* ignore */ }
                    }
                }
            }
        }
        catch
        {
        }

        return roots;
    }

    private async Task OnBranchChangedAsync(string repoPath, string newBranchOrHead)
    {
        try
        {
            Log(l =>
            {
                l.Info($"[CodeScene] Branch changed in repo '{repoPath}': '{newBranchOrHead}'. Clearing delta cache + delta review…");
                return Task.CompletedTask;
            });

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var review = await VS.GetMefServiceAsync<IReviewService>();
            if (review != null)
            {
                await review.DeltaReviewOpenDocsAsync();
            }
        }
        catch (Exception ex)
        {
            Log(l =>
            {
                l.Error($"[CodeScene] Failed handling branch switch in '{repoPath}' -> {newBranchOrHead}", ex);
                return Task.CompletedTask;
            });
        }
    }

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
        _ = RebuildRepoWatchersAsync();
        return VSConstants.S_OK;
    }

    public int OnAfterCloseSolution(object pUnkReserved)
    {
        try
        {
            new DeltaCacheService().Clear();
            CodeSceneToolWindow.UpdateViewAsync().FireAndForget();

            Log(l =>
            {
                l.Info("[CodeScene] Solution or folder closed. Cleared delta cache.");
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            Log(l =>
            {
                l.Error("[CodeScene] Unable to clear delta cache on close.", ex);
                return Task.CompletedTask;
            });
        }

        StopAllWatchers();
        return VSConstants.S_OK;
    }

    public int OnBeforeCloseSolution(object pUnkReserved)
    {
        StopAllWatchers();
        return VSConstants.S_OK;
    }

    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.S_OK;
    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) { pfCancel = 0; return VSConstants.S_OK; }
    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;
    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;
    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) { pfCancel = 0; return VSConstants.S_OK; }
    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;
    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) { pfCancel = 0; return VSConstants.S_OK; }

    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
        _ = RebuildRepoWatchersAsync();
        return VSConstants.S_OK;
    }

    public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

    public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
    {
        _ = RebuildRepoWatchersAsync();
        return VSConstants.S_OK;
    }

    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
    {
        if (fFirstShow != 0) _ = RebuildRepoWatchersAsync();
        return VSConstants.S_OK;
    }

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
    {
        _ = RebuildRepoWatchersAsync();
        return VSConstants.S_OK;
    }

    private void StopAllWatchers()
    {
        lock (_sync)
        {
            foreach (var w in _watchers.Values) w.Dispose();
            _watchers.Clear();
        }
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (_solutionCookie != 0 && _solution != null)
        {
            _solution.UnadviseSolutionEvents(_solutionCookie);
            _solutionCookie = 0;
        }

        if (_rdtCookie != 0 && _rdt != null)
        {
            _rdt.UnadviseRunningDocTableEvents(_rdtCookie);
            _rdtCookie = 0;
        }

        StopAllWatchers();
    }

    private static void Log(Func<ILogger, Task> logAction)
    {
        _ = Task.Run(async () =>
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();
            if (logger != null) await logAction(logger);
        });
    }

    private static class GitRepoHelper
    {
        public static string FindRepoRootForFile(string filePath)
        {
            try
            {
                var dir = new DirectoryInfo(Path.GetDirectoryName(filePath)!);
                while (dir != null)
                {
                    var dotGit = Path.Combine(dir.FullName, ".git");
                    if (Directory.Exists(dotGit) || File.Exists(dotGit))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch {}
            return null;
        }

        public static string ResolveGitDir(string repoRoot)
        {
            if (string.IsNullOrWhiteSpace(repoRoot)) return null;
            var dotGit = Path.Combine(repoRoot, ".git");

            if (Directory.Exists(dotGit)) return dotGit;

            if (File.Exists(dotGit))
            {
                var text = SafeReadAllText(dotGit);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    const string prefix = "gitdir:";
                    var line = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                   .FirstOrDefault(l => l.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(line))
                    {
                        var p = line.Substring(line.IndexOf(':') + 1).Trim();
                        if (!Path.IsPathRooted(p)) p = Path.GetFullPath(Path.Combine(repoRoot, p));
                        if (Directory.Exists(p)) return p;
                    }
                }
            }
            return null;
        }

        public static string GetHeadPath(string repoRoot)
        {
            var gitDir = ResolveGitDir(repoRoot);
            if (string.IsNullOrEmpty(gitDir)) return null;
            var head = Path.Combine(gitDir, "HEAD");
            return File.Exists(head) ? head : null;
        }


        public static string ParseHeadRefOrHash(string headText)
        {
            if (string.IsNullOrWhiteSpace(headText)) return string.Empty;
            headText = headText.Trim();
            const string prefix = "ref: ";
            if (headText.StartsWith(prefix, StringComparison.Ordinal))
            {
                return headText.Substring(prefix.Length).Trim();
            }
            return headText;
        }

        private static string SafeReadAllText(string path)
        {
            for (int i = 0; i < 3; i++)
            {
                try { return File.ReadAllText(path); }
                catch { System.Threading.Thread.Sleep(25); }
            }
            return null;
        }
    }

    private sealed class HeadWatcher : IDisposable
    {
        private readonly string _repoRoot;
        private readonly Func<string, string, Task> _onBranchChanged;
        private FileSystemWatcher _watcher;
        private string _lastHeadValue;

        public HeadWatcher(string repoRoot, Func<string, string, Task> onBranchChanged)
        {
            _repoRoot = repoRoot;
            _onBranchChanged = onBranchChanged;
        }

        public void EnsureWatching()
        {
            var headPath = GitRepoHelper.GetHeadPath(_repoRoot);
            if (string.IsNullOrEmpty(headPath)) { Dispose(); return; }

            var headDir = Path.GetDirectoryName(headPath);
            var headFile = Path.GetFileName(headPath);

            if (string.IsNullOrEmpty(headDir) || string.IsNullOrEmpty(headFile) || !Directory.Exists(headDir))
            {
                Dispose();
                return;
            }

            try
            {
                if (_watcher == null)
                {
                    _watcher = new FileSystemWatcher(headDir)
                    {
                        Filter = headFile,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                    };
                    _watcher.Changed += OnHeadChanged;
                    _watcher.Created += OnHeadChanged;
                    _watcher.Renamed += OnHeadChanged;
                    _watcher.Deleted += OnHeadChanged;
                }

                _lastHeadValue = ReadHeadValue(Path.Combine(headDir, headFile));
                _watcher.EnableRaisingEvents = true;
            }
            catch
            {
                Dispose();
            }
        }


        private void OnHeadChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                var newVal = ReadHeadValue(e.FullPath);
                if (!string.Equals(newVal, _lastHeadValue, StringComparison.Ordinal))
                {
                    _lastHeadValue = newVal;
                    var parsed = GitRepoHelper.ParseHeadRefOrHash(newVal);
                    _ = _onBranchChanged(_repoRoot, parsed ?? "(detached HEAD)");
                }
            }
            catch {}
        }

        private static string ReadHeadValue(string headPath)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(headPath))
                        return File.ReadAllText(headPath).Trim();
                    break;
                }
                catch { System.Threading.Thread.Sleep(25); }
            }
            return string.Empty;
        }

        public void Dispose()
        {
            try
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Changed -= OnHeadChanged;
                    _watcher.Created -= OnHeadChanged;
                    _watcher.Renamed -= OnHeadChanged;
                    _watcher.Deleted -= OnHeadChanged;
                    _watcher.Dispose();
                }
            }
            catch {}
            finally { _watcher = null; }
        }
    }
}
