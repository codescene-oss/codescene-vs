// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class FileChangeHandler
    {
        private readonly ILogger _logger;
        private readonly ICodeReviewer _codeReviewer;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly TrackerManager _trackerManager;
        private readonly string _gitRootPath;
        private readonly Action<string> _onFileDeletedCallback;
        private readonly IGitService _gitService;
        private readonly IOpenDocumentContentProvider _openDocumentContentProvider;
        private IReadOnlyCollection<string> _workspacePaths;

        public FileChangeHandler(
            ILogger logger,
            ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker,
            IReadOnlyCollection<string> workspacePaths,
            TrackerManager trackerManager,
            IGitService gitService,
            string gitRootPath = null,
            Action<string> onFileDeletedCallback = null,
            IOpenDocumentContentProvider openDocumentContentProvider = null)
        {
            _logger = logger;
            _codeReviewer = codeReviewer;
            _supportedFileChecker = supportedFileChecker;
            _workspacePaths = workspacePaths ?? Array.Empty<string>();
            _trackerManager = trackerManager;
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _gitRootPath = gitRootPath;
            _onFileDeletedCallback = onFileDeletedCallback;
            _openDocumentContentProvider = openDocumentContentProvider;
        }

        public event EventHandler<string> FileDeletedFromGit;

        public void SetWorkspacePaths(IReadOnlyCollection<string> workspacePaths)
        {
            _workspacePaths = workspacePaths ?? Array.Empty<string>();
        }

        public async Task HandleFileChangeAsync(string filePath, List<string> changedFiles, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var isDirectory = !Path.HasExtension(filePath);
            if (isDirectory)
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> FileChangeHandler: Skipping directory: {filePath}");
                #endif
                return;
            }

            if (!ShouldProcessFile(filePath, changedFiles))
            {
                if (_trackerManager.Contains(filePath))
                {
                    _trackerManager.Remove(filePath);
                    FireFileDeletedFromGit(filePath);
                }

                return;
            }

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> FileChangeHandler: Processing file change: {filePath}");
            #endif
            _trackerManager.Add(filePath);

            await ReviewFileAsync(filePath, operationGeneration, cancellationToken);
        }

        public async Task HandleFileDeleteAsync(string filePath, List<string> changedFiles, CancellationToken cancellationToken = default)
        {
            await Task.Run(
                () =>
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> FileChangeHandler: Processing file delete: {filePath}");
                #endif
                var wasTracked = _trackerManager.Contains(filePath);
                if (wasTracked)
                {
                    _trackerManager.Remove(filePath);
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

                    var filesToDelete = _trackerManager.GetFilesStartingWith(directoryPrefix);
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> FileChangeHandler: Handling directory delete '{filePath}' - removing {filesToDelete.Count} files");
                    #endif
                    _trackerManager.RemoveAll(filesToDelete);

                    foreach (var fileToDelete in filesToDelete)
                    {
                        FireFileDeletedFromGit(fileToDelete);
                    }
                }
            },
                cancellationToken);
        }

        public bool ShouldProcessFile(string filePath, List<string> changedFiles)
        {
            if (_gitService.IsFileIgnored(filePath))
            {
                return false;
            }

            if (!_supportedFileChecker.IsSupported(filePath))
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> FileChangeHandler: File not processed - unsupported file type: {filePath}");
                #endif
                return false;
            }

            if (!IsFileInChangedList(filePath, changedFiles))
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> FileChangeHandler: File not processed - not in changed list: {filePath}. Changed list ({changedFiles?.Count ?? 0} files): {string.Join(", ", changedFiles ?? new List<string>())}");
                #endif
                return false;
            }

            return true;
        }

        public async Task ReviewFileAsync(string filePath, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(filePath))
                {
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> FileChangeHandler: File does not exist for review: {filePath}");
                    #endif
                    return;
                }

                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> FileChangeHandler: Starting review for file: {filePath}");
                #endif
                string content = null;
                if (_openDocumentContentProvider != null)
                {
                    try
                    {
                        content = await _openDocumentContentProvider.GetContentForReviewAsync(filePath, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"GitChangeObserver: Open document provider failed for {filePath}: {ex.Message}");
                    }
                }

                content ??= File.ReadAllText(filePath);

                cancellationToken.ThrowIfCancellationRequested();
                var (review, delta) = await _codeReviewer.ReviewWithDeltaAsync(filePath, content, operationGeneration, cancellationToken).ConfigureAwait(false);

                if (review != null)
                {
                    _logger?.Debug($"GitChangeObserver: File reviewed: {filePath}");
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> FileChangeHandler: Completed review for file: {filePath}");
                    #endif
                }
                else
                {
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> FileChangeHandler: Review returned null for file: {filePath}");
                    #endif
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Could not load file for review {filePath}: {ex.Message}");
            }
        }

        private bool IsFileInChangedList(string filePath, List<string> changedFiles)
        {
            if (_workspacePaths == null || _workspacePaths.Count == 0)
            {
                return true;
            }

            if (!IsInAnyWorkspace(filePath))
            {
                return false;
            }

            var pathToMatch = string.IsNullOrEmpty(_gitRootPath)
                ? filePath
                : PathUtilities.GetRelativePath(_gitRootPath, filePath);
            var normalizedPathToMatch = pathToMatch.Replace('\\', '/');

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> FileChangeHandler: Checking if file is in changed list - path: '{normalizedPathToMatch}'");
            #endif

            if (changedFiles == null)
            {
                return true;
            }

            return changedFiles.Any(cf => cf.Replace('\\', '/').Equals(normalizedPathToMatch, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsInAnyWorkspace(string filePath)
        {
            return GitPathHelper.IsPathUnderAnyRoot(filePath, _workspacePaths);
        }

        private void FireFileDeletedFromGit(string filePath)
        {
            try
            {
                _logger?.Debug($"GitChangeObserver: File deleted from git: {filePath}");

                FileDeletedFromGit?.Invoke(this, filePath);
                _onFileDeletedCallback?.Invoke(filePath);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Error firing file deleted event: {ex.Message}");
            }
        }
    }
}
