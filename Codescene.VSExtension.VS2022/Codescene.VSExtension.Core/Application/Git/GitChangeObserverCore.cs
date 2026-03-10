// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Enums.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Util;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitChangeObserverCore : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ICodeReviewer _codeReviewer;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly IAsyncTaskScheduler _taskScheduler;
        private readonly IGitChangeLister _gitChangeLister;
        private readonly IGitService _gitService;

        private TrackerManager _trackerManager;
        private FileSystemWatcher _fileWatcher;
        private FileSystemEventHandler _watcherCreatedHandler;
        private FileSystemEventHandler _watcherChangedHandler;
        private FileSystemEventHandler _watcherDeletedHandler;

        private FileChangeEventProcessor _eventProcessor;
        private string _solutionPath;
        private string _workspacePath;
        private string _gitRootPath;
        private CancellationTokenSource _cts;

        private ISavedFilesTracker _savedFilesTracker;
        private IOpenFilesObserver _openFilesObserver;
        private GitChangeDetector _gitChangeDetector;
        private FileChangeHandler _fileChangeHandler;
        private Func<Task<List<string>>> _getChangedFilesCallback;
        private CodeHealthRulesWatcher _rulesWatcher;

        public GitChangeObserverCore(
            ILogger logger,
            ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker,
            IAsyncTaskScheduler taskScheduler,
            IGitChangeLister gitChangeLister,
            IGitService gitService)
        {
            _logger = logger;
            _codeReviewer = codeReviewer;
            _supportedFileChecker = supportedFileChecker;
            _taskScheduler = taskScheduler;
            _gitChangeLister = gitChangeLister;
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _trackerManager = new TrackerManager(_logger);
        }

        public event EventHandler<string> FileDeletedFromGit;

        public event EventHandler ViewUpdateRequested;

        public ConcurrentQueue<FileChangeEvent> EventQueue => _eventProcessor?.EventQueue;

        public FileSystemWatcher FileWatcher => _fileWatcher;

        public Timer ScheduledTimer => _eventProcessor?.ScheduledTimer;

        public void Initialize(string solutionPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver, Func<Task<List<string>>> getChangedFilesCallback = null, IOpenDocumentContentProvider openDocumentContentProvider = null)
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
            _gitChangeDetector = new GitChangeDetector(_logger, _supportedFileChecker, _gitService);

            _eventProcessor = new FileChangeEventProcessor(_logger, _taskScheduler, ProcessEventAsync, _getChangedFilesCallback);
            _cts = new CancellationTokenSource();

            InitializeGitPaths();

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Initialized with solution='{_solutionPath}', gitRoot='{_gitRootPath}', workspace='{_workspacePath}'");
            #endif

            _gitChangeLister.Initialize(_gitRootPath, _workspacePath);
            _gitChangeLister.FilesDetected += OnGitChangeListerFilesDetected;

            _fileChangeHandler = new FileChangeHandler(_logger, _codeReviewer, _supportedFileChecker, _workspacePath, _trackerManager, _gitService, OnFileDeleted, openDocumentContentProvider, () => _openFilesObserver?.GetActiveDocumentPath());
            _fileChangeHandler.FileDeletedFromGit += (sender, args) => FileDeletedFromGit?.Invoke(this, args);

            if (!string.IsNullOrEmpty(_workspacePath) && Directory.Exists(_workspacePath))
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeObserverCore: Creating file watcher for path '{_workspacePath}'");
                #endif
                _fileWatcher = GitPathDiscovery.CreateWatcher(_workspacePath);
            }

            _rulesWatcher = new CodeHealthRulesWatcher(_gitRootPath, _logger);
            _rulesWatcher.RulesFileChanged += (sender, args) => ViewUpdateRequested?.Invoke(this, EventArgs.Empty);

            InitializeTracker();
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

            _eventProcessor.Start(TimeSpan.FromSeconds(1), _cts.Token);

#if FEATURE_PERIODIC_GIT_SCAN
            _gitChangeLister.StartPeriodicScanning(_cts.Token);
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeObserverCore: Started file watcher and timer with 1 second interval");
            #endif
#endif
        }

        public virtual async Task<List<string>> GetChangedFilesVsBaselineAsync()
        {
            return await _gitChangeDetector.GetChangedFilesVsBaselineAsync(_gitRootPath, _workspacePath, _savedFilesTracker, _openFilesObserver);
        }

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
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeObserverCore: Disposing and cleaning up resources");
            #endif
            _gitChangeLister.FilesDetected -= OnGitChangeListerFilesDetected;
            _gitChangeLister.StopPeriodicScanning();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (_fileWatcher != null)
            {
                try
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    if (_watcherCreatedHandler != null)
                    {
                        _fileWatcher.Created -= _watcherCreatedHandler;
                    }

                    if (_watcherChangedHandler != null)
                    {
                        _fileWatcher.Changed -= _watcherChangedHandler;
                    }

                    if (_watcherDeletedHandler != null)
                    {
                        _fileWatcher.Deleted -= _watcherDeletedHandler;
                    }
                }
                catch
                {
                    // ignored
                }

                _fileWatcher?.Dispose();
                _fileWatcher = null;
            }

            _rulesWatcher?.Dispose();
            _rulesWatcher = null;

            _eventProcessor?.Dispose();
            _eventProcessor = null;
        }

        public void CancelAndReset()
        {
            _gitChangeLister.FilesDetected -= OnGitChangeListerFilesDetected;
            _gitChangeLister.StopPeriodicScanning();
            _eventProcessor?.DrainAndStop();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _gitChangeLister.FilesDetected += OnGitChangeListerFilesDetected;
            _eventProcessor?.Start(TimeSpan.FromSeconds(1), _cts.Token);
            #if FEATURE_PERIODIC_GIT_SCAN
            _gitChangeLister.StartPeriodicScanning(_cts.Token);
            #endif
        }

        private void OnFileDeleted(string filePath)
        {
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Invalidating delta cache for deleted file '{filePath}'");
            #endif
            ReviewCacheCleanup.InvalidateFile(filePath);
            ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnGitChangeListerFilesDetected(object sender, HashSet<string> absolutePaths)
        {
            var cts = _cts;
            if (cts == null)
            {
                return;
            }

            var token = cts.Token;
            _taskScheduler.Schedule(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: GitChangeLister detected {absolutePaths.Count} files");
                    #endif

                    await ProcessFilesAsync(absolutePaths, token);

                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: Processed detected files");
                    #endif
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error processing detected files: {ex.Message}");
                }
            });
        }

        private void InitializeGitPaths()
        {
            try
            {
                (_workspacePath, _gitRootPath) = GitPathDiscovery.Discover(_solutionPath);
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeObserverCore: Git repository discovered successfully at '{_gitRootPath}'");
                #endif
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Could not discover git repository: {ex.Message}");
                _gitRootPath = _workspacePath = Directory.Exists(_solutionPath) ? _solutionPath : Path.GetDirectoryName(_solutionPath);
            }
        }

        private void InitializeTracker()
        {
            var cts = _cts;
            if (cts == null)
            {
                return;
            }

            var token = cts.Token;
            _taskScheduler.Schedule(async () =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    var absolutePaths = await _gitChangeLister.CollectFilesFromRepoStateAsync(_gitRootPath, _workspacePath);
                    var changedFiles = await _getChangedFilesCallback();

                    // Add all files to tracker unconditionally - this ensures HandleFileDelete works correctly.
                    // Files open in the editor are excluded from changedFiles (via OpenFilesObserver), but they
                    // still need to be tracked so that delete events are properly handled.
                    foreach (var absolutePath in absolutePaths)
                    {
                        token.ThrowIfCancellationRequested();
                        _trackerManager.Add(absolutePath);

                        if (File.Exists(absolutePath) && _fileChangeHandler.ShouldProcessFile(absolutePath, changedFiles))
                        {
                            await _fileChangeHandler.ReviewFileAsync(absolutePath, token);
                        }
                    }

#if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: Initialized tracker");
#endif
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error initializing tracker: {ex.Message}");
                }
            });
        }

        private async Task ProcessFilesAsync(IEnumerable<string> absolutePaths, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var changedFiles = await _getChangedFilesCallback();
            foreach (var absolutePath in absolutePaths)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!File.Exists(absolutePath))
                {
                    continue;
                }

                await _fileChangeHandler.HandleFileChangeAsync(absolutePath, changedFiles, cancellationToken);
            }
        }

        private void BindWatcherEvents(FileSystemWatcher watcher)
        {
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeObserverCore: Binding file watcher events");
            #endif
            _watcherCreatedHandler += (sender, e) =>
            {
#if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeObserverCore: File created event enqueued: '{e.FullPath}'");
#endif
                if (ShouldEnqueueEvent(e))
                {
                    _eventProcessor?.EnqueueEvent(new FileChangeEvent(FileChangeType.Create, e.FullPath));
                }
            };
            _watcherChangedHandler += (sender, e) =>
            {
                if (ShouldEnqueueEvent(e))
                {
                    _eventProcessor?.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, e.FullPath));
                }
            };
            _watcherDeletedHandler += (sender, e) =>
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeObserverCore: File deleted event enqueued: '{e.FullPath}'");
#endif
                if (ShouldEnqueueEvent(e))
                {
                    _eventProcessor?.EnqueueEvent(new FileChangeEvent(FileChangeType.Delete, e.FullPath));
                }
            };

            watcher.Created += _watcherCreatedHandler;
            watcher.Changed += _watcherChangedHandler;
            watcher.Deleted += _watcherDeletedHandler;
        }

        private bool ShouldEnqueueEvent(FileSystemEventArgs e)
        {
            return !_gitService.IsFileIgnored(e.FullPath) && Path.HasExtension(e.FullPath);
        }

        private async Task ProcessEventAsync(FileChangeEvent evt, List<string> changedFiles, CancellationToken cancellationToken)
        {
            if (evt.Type == FileChangeType.Delete)
            {
                await _fileChangeHandler.HandleFileDeleteAsync(evt.FilePath, changedFiles, cancellationToken);
            }
            else
            {
                await _fileChangeHandler.HandleFileChangeAsync(evt.FilePath, changedFiles, cancellationToken);
            }
        }
    }
}
