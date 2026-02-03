// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Enums.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitChangeObserverCore : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ICodeReviewer _codeReviewer;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly IGitService _gitService;
        private readonly IAsyncTaskScheduler _taskScheduler;

        private FileSystemWatcher _fileWatcher;
        private Timer _scheduledTimer;
        private string _solutionPath;
        private string _workspacePath;
        private string _gitRootPath;

        private readonly TrackerManager _trackerManager = new TrackerManager();

        private readonly ConcurrentQueue<FileChangeEvent> _eventQueue = new ConcurrentQueue<FileChangeEvent>();

        private ISavedFilesTracker _savedFilesTracker;
        private IOpenFilesObserver _openFilesObserver;
        private GitChangeDetector _gitChangeDetector;
        private FileChangeHandler _fileChangeHandler;
        private Func<Task<List<string>>> _getChangedFilesCallback;

        public GitChangeObserverCore(ILogger logger, ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker, IGitService gitService,
            IAsyncTaskScheduler taskScheduler)
        {
            _logger = logger;
            _codeReviewer = codeReviewer;
            _supportedFileChecker = supportedFileChecker;
            _gitService = gitService;
            _taskScheduler = taskScheduler;
        }

        public ConcurrentQueue<FileChangeEvent> EventQueue => _eventQueue;

        public FileSystemWatcher FileWatcher => _fileWatcher;

        public Timer ScheduledTimer => _scheduledTimer;

        public void Initialize(string solutionPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver, Func<Task<List<string>>> getChangedFilesCallback = null)
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
            _getChangedFilesCallback = getChangedFilesCallback ?? GetChangedFilesVsBaselineAsync;
            _gitChangeDetector = new GitChangeDetector(_logger, _supportedFileChecker);

            InitializeGitPaths();

            _fileChangeHandler = new FileChangeHandler(_logger, _codeReviewer, _supportedFileChecker, _workspacePath, _trackerManager);
            _fileChangeHandler.FileDeletedFromGit += (sender, args) => FileDeletedFromGit?.Invoke(this, args);

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
            _taskScheduler.Schedule(async () =>
            {
                try
                {
                    var files = await CollectFilesFromRepoStateAsync();
                    foreach (var file in files)
                    {
                        _trackerManager.Add(file);
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
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
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
            _taskScheduler.Schedule(async () =>
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

            var changedFiles = await _getChangedFilesCallback();

            foreach (var evt in events)
            {
                if (evt.Type == FileChangeType.Delete)
                {
                    await _fileChangeHandler.HandleFileDeleteAsync(evt.FilePath, changedFiles);
                }
                else
                {
                    await _fileChangeHandler.HandleFileChangeAsync(evt.FilePath, changedFiles);
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
                    var changedFiles = await _getChangedFilesCallback();

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

        public event EventHandler<string> FileDeletedFromGit;

        public void RemoveFromTracker(string filePath)
        {
            _trackerManager.Remove(filePath);
        }

        public TrackerManager GetTrackerManager()
        {
            return _trackerManager;
        }

        public async Task HandleFileChangeForTestingAsync(string filePath, List<string> changedFiles)
        {
            await _fileChangeHandler.HandleFileChangeAsync(filePath, changedFiles);
        }

        public async Task HandleFileDeleteForTestingAsync(string filePath, List<string> changedFiles)
        {
            await _fileChangeHandler.HandleFileDeleteAsync(filePath, changedFiles);
        }

        public bool ShouldProcessFileForTesting(string filePath, List<string> changedFiles)
        {
            return _fileChangeHandler.ShouldProcessFile(filePath, changedFiles);
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
