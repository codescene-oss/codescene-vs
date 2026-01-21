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
        private GitChangeDetector _gitChangeDetector;

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
            _gitChangeDetector = new GitChangeDetector(_logger, _supportedFileChecker);

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

        // ThreadHelper.JoinableTaskFactory requires VS services to be initialized and is null in unit test environments
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
            return await _gitChangeDetector.GetChangedFilesVsBaselineAsync(_gitRootPath, _savedFilesTracker, _openFilesObserver);
        }


        private async Task<List<string>> CollectFilesFromRepoStateAsync()
        {
            return await Task.Run(async () =>
            {
                var files = new List<string>();

                try
                {
                    var changedFiles = await GetChangedFilesVsBaselineAsync();

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

            var relativePath = PathUtilities.GetRelativePath(_workspacePath, filePath);

            var normalizedRelativePath = relativePath.Replace('\\', '/');

            return changedFiles.Any(cf => cf.Replace('\\', '/').Equals(normalizedRelativePath, StringComparison.OrdinalIgnoreCase));
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
}
