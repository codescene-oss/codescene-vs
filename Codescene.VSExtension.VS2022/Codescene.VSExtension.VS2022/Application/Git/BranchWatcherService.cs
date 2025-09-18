using Codescene.VSExtension.VS2022.Review;
using Community.VisualStudio.Toolkit;
using LibGit2Sharp;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.Git;

/// <summary>
/// Monitors changes to the current Git branch in the repository associated with a Visual Studio solution.
/// Watches the .git/HEAD file and triggers a callback when a branch switch is detected.
/// </summary>
public class BranchWatcherService : IDisposable
{
    private FileSystemWatcher _headWatcher;
    private FileSystemWatcher _branchWatcher;
    private string _repoPath;
    private string _gitDirPath;
    private string _headFilePath;
    private string _lastBranch;
    private string _lastCommit;
    private Action<string> _onBranchChanged;
    private FileSystemWatcher _reflogWatcher;
    private FileSystemWatcher _packedRefsWatcher;
    private DateTime _lastEventTs = DateTime.MinValue;

    /// <summary>
    /// Initializes branch monitoring for the Git repository containing the given solution.
    /// </summary>
    /// <param name="solutionPath">The full path to the solution file or folder.</param>
    /// <param name="onBranchChanged">
    /// A callback that is invoked with the new branch name whenever a branch switch is detected.
    /// </param>
    public void StartWatching(string solutionPath, Action<string> onBranchChanged)
    {
        InitializeGitInformation(solutionPath);

        if (!File.Exists(_headFilePath))
        {
            return;
        }

        _onBranchChanged = onBranchChanged;

        InitializeGitChangeMonitor();
        InitializeCommitChangeMonitor();
    }

    /// <summary>
    /// Discovers the Git repository for the given solution and sets internal paths for .git tracking.
    /// </summary>
    /// <param name="solutionPath">The full path to the Visual Studio solution or folder.</param>
    private void InitializeGitInformation(string solutionPath)
    {
        _repoPath = Repository.Discover(solutionPath);
        if (string.IsNullOrEmpty(_repoPath))
        {
            Debug.WriteLine("BranchWatcherService: No git repo found.");
            return;
        }

        using var repo = new Repository(_repoPath);

        _gitDirPath = repo.Info.Path;
        _headFilePath = Path.Combine(_gitDirPath, "HEAD");
        _lastBranch = ReadCurrentBranch();
    }

    /// <summary>
    /// Sets up the FileSystemWatcher to monitor the .git/HEAD file for changes.
    /// </summary>
    private void InitializeGitChangeMonitor()
    {
        _headWatcher = new FileSystemWatcher(_gitDirPath)
        {
            Filter = "HEAD",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };

        _headWatcher.Changed += OnHeadFileChanged;
        _headWatcher.Created += OnHeadFileChanged;
        _headWatcher.Renamed += OnHeadFileChanged;
        _headWatcher.EnableRaisingEvents = true;
    }

    private void InitializeCommitChangeMonitor()
    {
        DisposeCommitWatchers();

        var branchRefFile = Path.Combine(_gitDirPath, "refs", "heads", _lastBranch);
        var branchLogFile = Path.Combine(_gitDirPath, "logs", "refs", "heads", _lastBranch);
        var packedRefs = Path.Combine(_gitDirPath, "packed-refs");

        void watch(string dir, string filter, FileSystemEventHandler handler, out FileSystemWatcher w)
        {
            if (!Directory.Exists(dir)) { w = null; return; }
            w = new FileSystemWatcher(dir)
            {
                Filter = filter,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024
            };
            w.Changed += handler;
            w.Created += handler;
            w.Renamed += (s, e) => handler(s, e);
            w.Error += (s, e) => Debug.WriteLine($"FSW error {dir}\\{filter}: {e.GetException()?.Message}");
            w.EnableRaisingEvents = true;
        }

        if (File.Exists(branchRefFile))
            watch(Path.GetDirectoryName(branchRefFile), Path.GetFileName(branchRefFile), OnCommitIndicatorChanged, out _branchWatcher);

        if (File.Exists(branchLogFile))
            watch(Path.GetDirectoryName(branchLogFile), Path.GetFileName(branchLogFile), OnCommitIndicatorChanged, out _reflogWatcher);

        if (File.Exists(packedRefs))
            watch(Path.GetDirectoryName(packedRefs), Path.GetFileName(packedRefs), OnCommitIndicatorChanged, out _packedRefsWatcher);

        _lastCommit = GetHeadShaSafe(_gitDirPath); // initialize
    }

    private async void OnCommitIndicatorChanged(object sender, FileSystemEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastEventTs).TotalMilliseconds < 250) return; // debounce
        _lastEventTs = now;

        try
        {
            await Task.Delay(50); // let git finish writing

            var gitDir = _gitDirPath; // ensure this is the actual .git dir
            var sha = GetHeadShaSafe(gitDir);
            if (string.IsNullOrEmpty(sha) || sha.Equals(_lastCommit, StringComparison.OrdinalIgnoreCase))
                return;

            _lastCommit = sha;
            Debug.WriteLine($"New commit detected: {sha}");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var reviewService = await VS.GetMefServiceAsync<IReviewService>();
            await reviewService.DeltaReviewOpenDocsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Commit watcher handler error: {ex.Message}");
        }
    }

    private string GetHeadShaSafe(string gitDirOrRepoPath)
    {
        try
        {
            using var repo = new Repository(gitDirOrRepoPath);
            return repo.Head?.Tip?.Sha ?? "";
        }
        catch { return ""; }
    }

    private void DisposeCommitWatchers()
    {
        _branchWatcher?.Dispose(); _branchWatcher = null;
        _reflogWatcher?.Dispose(); _reflogWatcher = null;
        _packedRefsWatcher?.Dispose(); _packedRefsWatcher = null;
    }


    /// <summary>
    /// Handles file system events triggered when the HEAD file is modified.
    /// Detects a branch switch and invokes the registered callback if necessary.
    /// </summary>
    private async void OnHeadFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var current = ReadCurrentBranch();
            if (current != _lastBranch)
            {
                _lastBranch = current;
                Debug.WriteLine($"BranchWatcherService: Branch changed to {current}");
                _onBranchChanged?.Invoke(current);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BranchWatcherService: Error during HEAD check - {ex.Message}");
        }
    }

    private async void OnBranchRefChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var commitSha = File.ReadAllText(e.FullPath).Trim();
            if (commitSha != _lastCommit)
            {
                _lastCommit = commitSha;
                Debug.WriteLine($"New commit detected: {commitSha}");

                // Trigger delta review
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var _reviewService = await VS.GetMefServiceAsync<IReviewService>();
                await _reviewService.DeltaReviewOpenDocsAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error watching branch ref file.", ex);
        }
    }

    /// <summary>
    /// Reads the current branch from the .git/HEAD file.
    /// </summary>
    /// <returns>The name of the currently checked-out branch, or "(detached)" if in detached HEAD state.</returns>
    private string ReadCurrentBranch()
    {
        if (!File.Exists(_headFilePath))
            return "(missing)";

        var content = File.ReadAllText(_headFilePath).Trim();
        if (content.StartsWith("ref:"))
        {
            return content.Replace("ref: refs/heads/", "").Trim();
        }

        return "(detached)";
    }

    public void Stop()
    {
        _headWatcher?.Dispose();
        _headWatcher = null;
    }

    public void Dispose()
    {
        Stop();
    }
}