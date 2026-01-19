using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    [Export(typeof(IGitChangeObserver))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class GitChangeObserver : IGitChangeObserver, IDisposable
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly ICodeReviewer _codeReviewer;

        [Import]
        private readonly ISupportedFileChecker _supportedFileChecker;

        [Import]
        private readonly IGitService _gitService;

        private FileSystemWatcher _fileWatcher;
        private Timer _scheduledTimer;
        private string _solutionPath;
        private string _workspacePath;
        private string _gitRootPath;

        private readonly HashSet<string> _tracker = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _trackerLock = new object();

        private readonly ConcurrentQueue<FileChangeEvent> _eventQueue = new ConcurrentQueue<FileChangeEvent>();

        private ISavedFilesTracker _savedFilesTracker;
        private IOpenFilesObserver _openFilesObserver;

        public void Initialize(string solutionPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            if (savedFilesTracker == null)
            {
                throw new ArgumentNullException(nameof(savedFilesTracker), "SavedFilesTracker must be provided to GitChangeObserver");
            }

            if (openFilesObserver == null)
            {
                throw new ArgumentNullException(nameof(openFilesObserver), "OpenFilesObserver must be provided to GitChangeObserver");
            }

            _solutionPath = solutionPath;
            _savedFilesTracker = savedFilesTracker;
            _openFilesObserver = openFilesObserver;

            InitializeGitPaths();

            if (!string.IsNullOrEmpty(_workspacePath) && Directory.Exists(_workspacePath))
            {
                _fileWatcher = CreateWatcher(_workspacePath);
            }

            InitializeTracker();
        }

        private void InitializeGitPaths()
        {
            try
            {
                if (Directory.Exists(_solutionPath))
                {
                    _workspacePath = _solutionPath;
                }
                else
                {
                    _workspacePath = Path.GetDirectoryName(_solutionPath);
                }

                var repoPath = Repository.Discover(_solutionPath);
                if (!string.IsNullOrEmpty(repoPath))
                {
                    using (var repo = new Repository(repoPath))
                    {
                        _gitRootPath = repo.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar);
                    }
                }
                else
                {
                    _gitRootPath = _workspacePath;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Could not discover git repository: {ex.Message}");
                if (Directory.Exists(_solutionPath))
                {
                    _workspacePath = _solutionPath;
                }
                else
                {
                    _workspacePath = Path.GetDirectoryName(_solutionPath);
                }
                _gitRootPath = _workspacePath;
            }
        }

        private void InitializeTracker()
        {
            Task.Run(async () =>
            {
                try
                {
                    var files = await CollectFilesFromRepoStateAsync();
                    lock (_trackerLock)
                    {
                        foreach (var file in files)
                        {
                            _tracker.Add(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error initializing tracker: {ex.Message}");
                }
            });
        }

        public void Start()
        {
            if (_fileWatcher == null)
            {
                _logger?.Warn($"GitChangeObserver: Cannot start - file watcher not initialized");
                return;
            }

            if (_fileWatcher.EnableRaisingEvents)
            {
                return;
            }

            BindWatcherEvents(_fileWatcher);
            _fileWatcher.EnableRaisingEvents = true;

            _scheduledTimer = new Timer(ProcessQueuedEventsCallback, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private FileSystemWatcher CreateWatcher(string path)
        {
            return new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };
        }

        private void BindWatcherEvents(FileSystemWatcher watcher)
        {
            watcher.Created += (sender, e) => _eventQueue.Enqueue(new FileChangeEvent(FileChangeType.Create, e.FullPath));
            watcher.Changed += (sender, e) => _eventQueue.Enqueue(new FileChangeEvent(FileChangeType.Change, e.FullPath));
            watcher.Deleted += (sender, e) => _eventQueue.Enqueue(new FileChangeEvent(FileChangeType.Delete, e.FullPath));
        }

        private void ProcessQueuedEventsCallback(object state)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ProcessQueuedEventsAsync();
                }).FileAndForget("GitChangeObserver/ProcessQueuedEvents");
            }
            catch
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await ProcessQueuedEventsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"GitChangeObserver: Error processing queued events: {ex.Message}");
                    }
                });
            }
        }

        private async Task ProcessQueuedEventsAsync()
        {
            var events = new List<FileChangeEvent>();

            while (_eventQueue.TryDequeue(out var evt))
            {
                events.Add(evt);
            }

            if (events.Count == 0)
            {
                return;
            }

            var changedFiles = await GetChangedFilesVsBaselineAsync();

            foreach (var evt in events)
            {

                if (evt.Type == FileChangeType.Delete)
                {
                    await HandleFileDeleteAsync(evt.FilePath, changedFiles);
                }
                else
                {
                    await HandleFileChangeAsync(evt.FilePath, changedFiles);
                }
            }
        }

        public virtual async Task<List<string>> GetChangedFilesVsBaselineAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var changedFiles = new List<string>();

                    if (string.IsNullOrEmpty(_gitRootPath) || !Directory.Exists(_gitRootPath))
                    {
                        return changedFiles;
                    }

                    var repoPath = Repository.Discover(_gitRootPath);
                    if (string.IsNullOrEmpty(repoPath))
                    {
                        return changedFiles;
                    }

                    using (var repo = new Repository(repoPath))
                    {
                        var baseCommit = GetMergeBaseCommit(repo);

                    if (baseCommit == null)
                    {
                        _logger?.Debug("GitChangeObserver: No merge base commit found, using working directory changes only");
                    }

                    var filesToExcludeFromHeuristic = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (_savedFilesTracker != null)
                    {
                        foreach (var file in _savedFilesTracker.GetSavedFiles())
                        {
                            filesToExcludeFromHeuristic.Add(file);
                        }
                    }
                    if (_openFilesObserver != null)
                    {
                        foreach (var file in _openFilesObserver.GetAllVisibleFileNames())
                        {
                            filesToExcludeFromHeuristic.Add(file);
                        }
                    }

                        var committedChanges = GetCommittedChanges(repo, baseCommit);
                        var statusChanges = GetStatusChanges(repo, filesToExcludeFromHeuristic);

                        changedFiles.AddRange(committedChanges);
                        changedFiles.AddRange(statusChanges);

                        return changedFiles.Distinct().ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error getting changed files: {ex.Message}");
                    return new List<string>();
                }
            });
        }

        private Commit GetMergeBaseCommit(Repository repo)
        {
            try
            {
                if (repo.Head == null || repo.Head.Tip == null)
                {
                    return null;
                }

                var currentBranch = repo.Head;

                var mainBranch = repo.Branches["main"] ?? repo.Branches["master"] ?? repo.Branches["origin/main"] ?? repo.Branches["origin/master"];

                if (mainBranch == null || mainBranch.Tip == null)
                {
                    return null;
                }

                if (currentBranch.FriendlyName == mainBranch.FriendlyName)
                {
                    return null;
                }

                var mergeBase = repo.ObjectDatabase.FindMergeBase(currentBranch.Tip, mainBranch.Tip);
                return mergeBase;
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Could not determine merge base: {ex.Message}");
                return null;
            }
        }

        private List<string> GetCommittedChanges(Repository repo, Commit baseCommit)
        {
            var changes = new List<string>();

            try
            {
                if (baseCommit == null || repo.Head?.Tip == null)
                {
                    return changes;
                }

                var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, repo.Head.Tip.Tree);

                foreach (var change in diff)
                {
                    var relativePath = change.Path;
                    var fullPath = Path.Combine(_gitRootPath, relativePath);

                    if (_supportedFileChecker.IsSupported(fullPath))
                    {
                        changes.Add(relativePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Error getting committed changes: {ex.Message}");
            }

            return changes;
        }

        private List<string> GetStatusChanges(Repository repo, HashSet<string> filesToExclude)
        {
            var changes = new List<string>();

            try
            {
                var status = repo.RetrieveStatus();

                foreach (var item in status)
                {
                    if (item.State == FileStatus.Unaltered || item.State == FileStatus.Ignored)
                    {
                        continue;
                    }

                    var fullPath = Path.Combine(_gitRootPath, item.FilePath);

                    if (filesToExclude.Contains(fullPath))
                    {
                        continue;
                    }

                    if (_supportedFileChecker.IsSupported(fullPath))
                    {
                        changes.Add(item.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Error getting status changes: {ex.Message}");
            }

            return changes;
        }

        private async Task<List<string>> CollectFilesFromRepoStateAsync()
        {
            return await Task.Run(() =>
            {
                var files = new List<string>();

                try
                {
                    var changedFiles = GetChangedFilesVsBaselineAsync().GetAwaiter().GetResult();

                    foreach (var relativePath in changedFiles)
                    {
                        var fullPath = Path.Combine(_gitRootPath, relativePath);
                        if (File.Exists(fullPath))
                        {
                            files.Add(fullPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error collecting files from repo state: {ex.Message}");
                }

                return files;
            });
        }

        private bool ShouldProcessFile(string filePath, List<string> changedFiles)
        {
            if (!_supportedFileChecker.IsSupported(filePath))
            {
                return false;
            }

            if (!IsFileInChangedList(filePath, changedFiles))
            {
                return false;
            }

            return true;
        }

        private bool IsFileInChangedList(string filePath, List<string> changedFiles)
        {
            if (string.IsNullOrEmpty(_workspacePath))
            {
                return true;
            }

            var relativePath = GetRelativePath(_workspacePath, filePath);

            var normalizedRelativePath = relativePath.Replace('\\', '/');

            return changedFiles.Any(cf => cf.Replace('\\', '/').Equals(normalizedRelativePath, StringComparison.OrdinalIgnoreCase));
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
            {
                return fullPath;
            }

            try
            {
                var baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
                var fullUri = new Uri(fullPath);
                var relativeUri = baseUri.MakeRelativeUri(fullUri);
                return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return fullPath;
            }
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }
            return path;
        }

        private async Task HandleFileChangeAsync(string filePath, List<string> changedFiles)
        {
            var isDirectory = !Path.HasExtension(filePath);
            if (isDirectory)
            {
                return;
            }

            if (!ShouldProcessFile(filePath, changedFiles))
            {
                return;
            }

            lock (_trackerLock)
            {
                _tracker.Add(filePath);
            }

            await Task.Run(() => ReviewFile(filePath));
        }

        private async Task HandleFileDeleteAsync(string filePath, List<string> changedFiles)
        {
            await Task.Run(() =>
            {
                bool wasTracked;
                lock (_trackerLock)
                {
                    wasTracked = _tracker.Contains(filePath);
                    if (wasTracked)
                    {
                        _tracker.Remove(filePath);
                    }
                }

                if (wasTracked)
                {
                    FireFileDeletedFromGit(filePath);
                    return;
                }

                if (ShouldProcessFile(filePath, changedFiles))
                {
                    FireFileDeletedFromGit(filePath);
                    return;
                }

                var isDirectory = !Path.HasExtension(filePath);
                if (isDirectory)
                {
                    var directoryPrefix = filePath.EndsWith(Path.DirectorySeparatorChar.ToString())
                        ? filePath
                        : filePath + Path.DirectorySeparatorChar;

                    List<string> filesToDelete;
                    lock (_trackerLock)
                    {
                        filesToDelete = _tracker.Where(tf => tf.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToList();

                        foreach (var fileToDelete in filesToDelete)
                        {
                            _tracker.Remove(fileToDelete);
                        }
                    }

                    foreach (var fileToDelete in filesToDelete)
                    {
                        FireFileDeletedFromGit(fileToDelete);
                    }
                }
            });
        }

        private void ReviewFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                var content = File.ReadAllText(filePath);
                var review = _codeReviewer.Review(filePath, content);

                if (review != null)
                {
                    _logger?.Debug($"GitChangeObserver: File reviewed: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Could not load file for review {filePath}: {ex.Message}");
            }
        }

        private void FireFileDeletedFromGit(string filePath)
        {
            try
            {
                _logger?.Debug($"GitChangeObserver: File deleted from git: {filePath}");


                FileDeletedFromGit?.Invoke(this, new FileDeletedEventArgs(filePath));
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Error firing file deleted event: {ex.Message}");
            }
        }

        public event EventHandler<FileDeletedEventArgs> FileDeletedFromGit;

        public void RemoveFromTracker(string filePath)
        {
            lock (_trackerLock)
            {
                _tracker.Remove(filePath);
            }
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;

            _scheduledTimer?.Dispose();
            _scheduledTimer = null;
        }
    }

    internal enum FileChangeType
    {
        Create,
        Change,
        Delete
    }

    internal class FileChangeEvent
    {
        public FileChangeType Type { get; }
        public string FilePath { get; }

        public FileChangeEvent(FileChangeType type, string filePath)
        {
            Type = type;
            FilePath = filePath;
        }
    }

    public class FileDeletedEventArgs : EventArgs
    {
        public string FilePath { get; }

        public FileDeletedEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }

    public interface ISavedFilesTracker
    {
        IEnumerable<string> GetSavedFiles();
    }

    public interface IOpenFilesObserver
    {
        IEnumerable<string> GetAllVisibleFileNames();
    }

    public interface IGitChangeObserver : IDisposable
    {
        void Initialize(string solutionPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver);
        void Start();
        Task<List<string>> GetChangedFilesVsBaselineAsync();
        void RemoveFromTracker(string filePath);
        event EventHandler<FileDeletedEventArgs> FileDeletedFromGit;
    }
}
