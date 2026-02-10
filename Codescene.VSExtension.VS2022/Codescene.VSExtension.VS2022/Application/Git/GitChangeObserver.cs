// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    [Export(typeof(IGitChangeObserver))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class GitChangeObserver : IGitChangeObserver, IDisposable
    {
        [Import]
        private ILogger _logger;

        [Import]
        private ICodeReviewer _codeReviewer;

        [Import]
        private ISupportedFileChecker _supportedFileChecker;

        [Import]
        private IGitService _gitService;

        [Import]
        private IAsyncTaskScheduler _taskScheduler;

        [Import]
        private IGitChangeLister _gitChangeLister;

        private GitChangeObserverCore _core;

        public GitChangeObserver()
        {
        }

        public GitChangeObserver(
            ILogger logger,
            ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker,
            IGitService gitService,
            IAsyncTaskScheduler taskScheduler,
            IGitChangeLister gitChangeLister)
        {
            _logger = logger;
            _codeReviewer = codeReviewer;
            _supportedFileChecker = supportedFileChecker;
            _gitService = gitService;
            _taskScheduler = taskScheduler;
            _gitChangeLister = gitChangeLister;
            InitializeCore();
        }

        public event EventHandler<string> FileDeletedFromGit;

        public ConcurrentQueue<FileChangeEvent> EventQueue => _core?.EventQueue;

        public FileSystemWatcher FileWatcher => _core?.FileWatcher;

        public Timer ScheduledTimer => _core?.ScheduledTimer;

        public void Initialize(string solutionPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            if (_core == null)
            {
                InitializeCore();
            }

            _core.Initialize(solutionPath, savedFilesTracker, openFilesObserver, async () => await GetChangedFilesVsBaselineAsync());
        }

        public void Start()
        {
            _core?.Start();
        }

        public virtual async Task<List<string>> GetChangedFilesVsBaselineAsync()
        {
            return await _core.GetChangedFilesVsBaselineAsync();
        }

        public void RemoveFromTracker(string filePath)
        {
            _core?.RemoveFromTracker(filePath);
        }

        public TrackerManager GetTrackerManager()
        {
            return _core?.GetTrackerManager();
        }

        public async Task HandleFileChangeForTestingAsync(string filePath, List<string> changedFiles)
        {
            await _core.HandleFileChangeForTestingAsync(filePath, changedFiles);
        }

        public async Task HandleFileDeleteForTestingAsync(string filePath, List<string> changedFiles)
        {
            await _core.HandleFileDeleteForTestingAsync(filePath, changedFiles);
        }

        public bool ShouldProcessFileForTesting(string filePath, List<string> changedFiles)
        {
            return _core.ShouldProcessFileForTesting(filePath, changedFiles);
        }

        public void Dispose()
        {
            _core?.Dispose();
        }

        private void InitializeCore()
        {
            _core = new GitChangeObserverCore(_logger, _codeReviewer, _supportedFileChecker, _gitService, _taskScheduler, _gitChangeLister);
            _core.FileDeletedFromGit += (sender, args) => FileDeletedFromGit?.Invoke(this, args);
        }
    }
}
