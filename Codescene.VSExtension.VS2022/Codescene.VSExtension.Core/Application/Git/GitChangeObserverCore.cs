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
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Util;
using LibGit2Sharp;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitChangeObserverCore : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ICodeReviewer _codeReviewer;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly IAsyncTaskScheduler _taskScheduler;
        private readonly IGitChangeLister _gitChangeLister;

        private TrackerManager _trackerManager;
        private FileSystemWatcher _fileWatcher;
        private FileChangeEventProcessor _eventProcessor;
        private string _solutionPath;
        private string _workspacePath;
        private string _gitRootPath;

        private ISavedFilesTracker _savedFilesTracker;
        private IOpenFilesObserver _openFilesObserver;
        private GitChangeDetector _gitChangeDetector;
        private FileChangeHandler _fileChangeHandler;
        private Func<Task<List<string>>> _getChangedFilesCallback;

        public GitChangeObserverCore(
            ILogger logger,
            ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker,
            IAsyncTaskScheduler taskScheduler,
            IGitChangeLister gitChangeLister)
        {
            _logger = logger;
            _codeReviewer = codeReviewer;
            _supportedFileChecker = supportedFileChecker;
            _taskScheduler = taskScheduler;
            _gitChangeLister = gitChangeLister;
            _trackerManager = new TrackerManager(_logger);
        }

        public event EventHandler<string> FileDeletedFromGit;

        public event EventHandler ViewUpdateRequested;

        public ConcurrentQueue<FileChangeEvent> EventQueue => _eventProcessor?.EventQueue;

        public FileSystemWatcher FileWatcher => _fileWatcher;

        public Timer ScheduledTimer => _eventProcessor?.ScheduledTimer;

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

            _eventProcessor = new FileChangeEventProcessor(_logger, _taskScheduler, ProcessEventAsync, _getChangedFilesCallback);

            InitializeGitPaths();

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Initialized with solution='{_solutionPath}', gitRoot='{_gitRootPath}', workspace='{_workspacePath}'");
            #endif

            _gitChangeLister.Initialize(_gitRootPath, _workspacePath);
            _gitChangeLister.FilesDetected += OnGitChangeListerFilesDetected;

            _fileChangeHandler = new FileChangeHandler(_logger, _codeReviewer, _supportedFileChecker, _workspacePath, _trackerManager, PerformDeltaAnalysisAsync, OnFileDeleted);
            _fileChangeHandler.FileDeletedFromGit += (sender, args) => FileDeletedFromGit?.Invoke(this, args);

            if (!string.IsNullOrEmpty(_workspacePath) && Directory.Exists(_workspacePath))
            {
                _fileWatcher = CreateWatcher(_workspacePath);
            }

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

            _eventProcessor.Start(TimeSpan.FromSeconds(1));

#if FEATURE_PERIODIC_GIT_SCAN
            _gitChangeLister.StartPeriodicScanning();
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeObserverCore: Started file watcher and timer with 1 second interval");
            #endif
#endif
        }

        public virtual async Task<List<string>> GetChangedFilesVsBaselineAsync()
        {
            return await _gitChangeDetector.GetChangedFilesVsBaselineAsync(_gitRootPath, _savedFilesTracker, _openFilesObserver);
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
            _gitChangeLister.StopPeriodicScanning();
            _gitChangeLister.FilesDetected -= OnGitChangeListerFilesDetected;

            _fileWatcher?.Dispose();
            _fileWatcher = null;

            _eventProcessor?.Dispose();
            _eventProcessor = null;
        }

        private async Task PerformDeltaAnalysisAsync(string filePath, string content, FileReviewModel review, string baselineRawScore = null)
        {
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Starting delta analysis for '{filePath}'");
            #endif
            var pendingJob = new Job
            {
                Type = JobTypes.DELTA,
                State = StateTypes.RUNNING,
                File = new Models.WebComponent.Data.File { FileName = filePath },
            };
            try
            {
                DeltaJobTracker.Add(pendingJob);
                ViewUpdateRequested?.Invoke(this, EventArgs.Empty);

                if (review?.RawScore != null)
                {
                    var delta = await _codeReviewer.DeltaAsync(review, content, baselineRawScore);
                    ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: Delta analysis completed for '{filePath}'");
                    #endif
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Error performing delta analysis: {ex.Message}");
            }
            finally
            {
                DeltaJobTracker.Remove(pendingJob);
                ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnFileDeleted(string filePath)
        {
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Invalidating delta cache for deleted file '{filePath}'");
            #endif
            var deltaCache = new Core.Application.Cache.Review.DeltaCacheService();
            deltaCache.Invalidate(filePath);
            var baselineCache = new Core.Application.Cache.Review.BaselineReviewCacheService();
            baselineCache.Invalidate(filePath);
            ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnGitChangeListerFilesDetected(object sender, HashSet<string> absolutePaths)
        {
            _taskScheduler.Schedule(async () =>
            {
                try
                {
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: GitChangeLister detected {absolutePaths.Count} files");
                    #endif

                    var changedFiles = await _getChangedFilesCallback();
                    var alreadyTrackedCount = 0;
                    var newFilesCount = 0;

                    foreach (var absolutePath in absolutePaths)
                    {
                        if (File.Exists(absolutePath) && !_trackerManager.Contains(absolutePath))
                        {
                            _trackerManager.Add(absolutePath);
                            await _fileChangeHandler.HandleFileChangeAsync(absolutePath, changedFiles);
                            newFilesCount++;
                        }
                        else
                        {
                            alreadyTrackedCount++;
                        }
                    }

                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: Processed detected files - {newFilesCount} new, {alreadyTrackedCount} already tracked");
                    #endif
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
                        #if FEATURE_INITIAL_GIT_OBSERVER
                        _logger?.Info($">>> GitChangeObserverCore: Git repository discovered successfully at '{_gitRootPath}'");
                        #endif
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
                    var absolutePaths = await _gitChangeLister.CollectFilesFromRepoStateAsync(_gitRootPath, _workspacePath);
                    var addedCount = 0;
                    var changedFiles = await _getChangedFilesCallback();
                    foreach (var absolutePath in absolutePaths)
                    {
                        if (File.Exists(absolutePath))
                        {
                            _trackerManager.Add(absolutePath);
                            var path = absolutePath;
                            _taskScheduler.Schedule(async () =>
                            {
                                await _fileChangeHandler.HandleFileChangeAsync(path, changedFiles);
                            });
                            addedCount++;
                        }
                    }

#if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: Initialized tracker with {addedCount} files");
#endif
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error initializing tracker: {ex.Message}");
                }
            });
        }

        private FileSystemWatcher CreateWatcher(string path)
        {
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Creating file watcher for path '{path}'");
            #endif
            return new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            };
        }

        private void BindWatcherEvents(FileSystemWatcher watcher)
        {
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeObserverCore: Binding file watcher events");
            #endif
            watcher.Created += (sender, e) =>
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeObserverCore: File created event enqueued: '{e.FullPath}'");
                #endif
                _eventProcessor.EnqueueEvent(new FileChangeEvent(FileChangeType.Create, e.FullPath));
            };
            watcher.Changed += (sender, e) =>
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeObserverCore: File changed event enqueued: '{e.FullPath}'");
                #endif
                _eventProcessor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, e.FullPath));
            };
            watcher.Deleted += (sender, e) =>
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeObserverCore: File deleted event enqueued: '{e.FullPath}'");
                #endif
                _eventProcessor.EnqueueEvent(new FileChangeEvent(FileChangeType.Delete, e.FullPath));
            };
        }

        private async Task ProcessEventAsync(FileChangeEvent evt, List<string> changedFiles)
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
}
