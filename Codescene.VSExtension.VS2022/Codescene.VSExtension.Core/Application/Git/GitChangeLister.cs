// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Util;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitChangeLister : IGitChangeLister, IDisposable
    {
        private readonly int _pollingInterval = 9; // Default value, calculated based on core count.
        private readonly ISavedFilesTracker _savedFilesTracker;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly ILogger _logger;
        private readonly IGitService _gitService;
        private readonly UntrackedFileProcessor _untrackedFileProcessor;
        private readonly MergeBaseFinder _mergeBaseFinder;

        private string _gitRootPath;
        private IReadOnlyCollection<string> _workspacePaths;
        private DroppingScheduledExecutor _scheduledExecutor;
        private bool _disposed = false;

        public GitChangeLister(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger,
            IGitService gitService,
            int? pollingInterval = null)
        {
            _savedFilesTracker = savedFilesTracker ?? throw new ArgumentNullException(nameof(savedFilesTracker));
            _supportedFileChecker = supportedFileChecker ?? throw new ArgumentNullException(nameof(supportedFileChecker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _untrackedFileProcessor = new UntrackedFileProcessor(_gitService, logger);
            _mergeBaseFinder = new MergeBaseFinder(logger);
            if (pollingInterval.HasValue && pollingInterval.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pollingInterval), pollingInterval.Value, "Polling interval must be greater than 0.");
            }

            _pollingInterval = pollingInterval ?? CalculatePollingInterval();
        }

        public event EventHandler<HashSet<string>> FilesDetected;

        public virtual async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath, CancellationToken cancellationToken = default)
        {
            return await GetAllChangedFilesAsync(gitRootPath, string.IsNullOrEmpty(workspacePath) ? null : new[] { workspacePath }, cancellationToken);
        }

        public virtual async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ExecuteGitOperationAsync(gitRootPath, workspacePaths, "getting all changed files", cancellationToken, repo =>
            {
                var statusFiles = CollectFilesFromRepoState(repo, gitRootPath, workspacePaths);
                var diffFiles = CollectFilesFromGitDiff(repo, gitRootPath, workspacePaths);
                var allFiles = new HashSet<string>(statusFiles);
                allFiles.UnionWith(diffFiles);
                return allFiles;
            });
            cancellationToken.ThrowIfCancellationRequested();
#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: GetAllChangedFilesAsync found {result.Count} files");
#endif
            return result;
        }

        public virtual Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath, CancellationToken cancellationToken = default)
        {
            return GetChangedFilesVsMergeBaseAsync(gitRootPath, string.IsNullOrEmpty(workspacePath) ? null : new[] { workspacePath }, cancellationToken);
        }

        public virtual Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default)
        {
            return ExecuteAndLogAsync(gitRootPath, workspacePaths, "getting changed files vs merge base", "GetChangedFilesVsMergeBaseAsync found", GetChangedFilesVsMergeBase, cancellationToken);
        }

        public void Initialize(string gitRootPath, IReadOnlyCollection<string> workspacePaths)
        {
            _gitRootPath = gitRootPath;
            _workspacePaths = workspacePaths ?? Array.Empty<string>();
#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: Initialized with gitRoot='{gitRootPath}', workspacePaths count={_workspacePaths.Count}");
#endif
        }

        public void SetWorkspacePaths(IReadOnlyCollection<string> workspacePaths)
        {
            _workspacePaths = workspacePaths ?? Array.Empty<string>();
        }

        public void StartPeriodicScanning(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (_scheduledExecutor != null)
            {
                _logger?.Warn("GitChangeLister: Periodic scanning already started");
                return;
            }

            _scheduledExecutor = new DroppingScheduledExecutor(
                PeriodicScanAsync,
                cancellationToken,
                TimeSpan.FromSeconds(_pollingInterval),
                _logger);

            _scheduledExecutor.Start();
#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: Started periodic scanning with {_pollingInterval} second interval");
#endif
        }

        public void StopPeriodicScanning()
        {
            _scheduledExecutor?.Stop();
            _scheduledExecutor = null;
#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeLister: Stopped periodic scanning");
#endif
        }

        public virtual Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default)
        {
            return ExecuteAndLogAsync(gitRootPath, workspacePaths, "collecting files from repo state", "CollectFilesFromRepoStateAsync collected", CollectFilesFromRepoState, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeLister: Disposing and cleaning up resources");
#endif
            StopPeriodicScanning();
            _scheduledExecutor?.Dispose();
            _scheduledExecutor = null;
            _disposed = true;
        }

        protected virtual HashSet<string> CollectFilesFromRepoState(Repository repo, string gitRootPath, IReadOnlyCollection<string> workspacePaths)
        {
            var changedFiles = new HashSet<string>();

            try
            {
                var status = repo.RetrieveStatus();
                var untrackedByDirectory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var savedFiles = new HashSet<string>(_savedFilesTracker.GetSavedFiles(), StringComparer.OrdinalIgnoreCase);

                foreach (var item in status)
                {
                    if (ShouldSkipStatusItem(item))
                    {
                        continue;
                    }

                    var absolutePath = GitPathHelper.ConvertToAbsolutePath(item.FilePath, gitRootPath);

                    if (!GitPathHelper.IsFileInWorkspace(item.FilePath, gitRootPath, workspacePaths) || !ShouldReviewFile(absolutePath))
                    {
                        continue;
                    }

                    if (_gitService.IsFileIgnored(absolutePath))
                    {
                        continue;
                    }

                    if (item.State == FileStatus.NewInWorkdir)
                    {
                        _untrackedFileProcessor.AddUntrackedFileToDirectory(item.FilePath, absolutePath, untrackedByDirectory);
                    }
                    else
                    {
                        changedFiles.Add(absolutePath);
                    }
                }

                _untrackedFileProcessor.ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);
                changedFiles.RemoveWhere(path => _gitService.IsFileIgnored(path));
#if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeLister: CollectFilesFromRepoState collected {changedFiles.Count} files from repo state");
#endif
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeLister: Error collecting files from repo state: {ex.Message}");
            }

            return changedFiles;
        }

        protected virtual HashSet<string> CollectFilesFromGitDiff(Repository repo, string gitRootPath, IReadOnlyCollection<string> workspacePaths)
        {
            try
            {
                var relativePaths = GetChangedFilesVsMergeBase(repo, gitRootPath, workspacePaths);
                var result = ConvertAndFilterPaths(relativePaths, gitRootPath);
#if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeLister: CollectFilesFromGitDiff collected {result.Count} files from git diff");
#endif
                return result;
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeLister: Error collecting files from git diff: {ex.Message}");
                return new HashSet<string>();
            }
        }

        protected HashSet<string> ConvertAndFilterPaths(IEnumerable<string> relativePaths, string gitRootPath)
        {
            var result = new HashSet<string>();
            foreach (var relativePath in relativePaths)
            {
                var absolutePath = GitPathHelper.ConvertToAbsolutePath(relativePath, gitRootPath);
                if (!File.Exists(absolutePath) || _gitService.IsFileIgnored(absolutePath))
                {
                    continue;
                }

                if (ShouldReviewFile(absolutePath))
                {
                    result.Add(absolutePath);
                }
            }

            return result;
        }

        /// <summary>
        /// Dynamically set polling interval based on performance of the machine.
        /// </summary>
        private static int CalculatePollingInterval()
        {
            var coreCount = Environment.ProcessorCount;
            if (coreCount >= 6)
            {
                return 9;
            }

            if (coreCount >= 3)
            {
                return 18;
            }

            return 32;
        }

        private async Task<HashSet<string>> ExecuteGitOperationAsync(
            string gitRootPath,
            IReadOnlyCollection<string> workspacePathsUnused,
            string operationName,
            CancellationToken cancellationToken,
            Func<Repository, HashSet<string>> operation)
        {
#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: Starting operation '{operationName}'");
#endif
            return await Task.Run(
            () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsValidGitRoot(gitRootPath))
                    {
                        return new HashSet<string>();
                    }

                    var repoPath = Repository.Discover(gitRootPath);
                    if (string.IsNullOrEmpty(repoPath))
                    {
                        return new HashSet<string>();
                    }

                    using (var repo = new Repository(repoPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return operation(repo);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeLister: Error {operationName}: {ex.Message}");
                    return new HashSet<string>();
                }
            },
            cancellationToken);
        }

        private async Task<HashSet<string>> ExecuteAndLogAsync(
            string gitRootPath,
            IReadOnlyCollection<string> workspacePaths,
            string operationName,
            string logLabel,
            Func<Repository, string, IReadOnlyCollection<string>, HashSet<string>> repoOperation,
            CancellationToken cancellationToken = default)
        {
            var result = await ExecuteGitOperationAsync(gitRootPath, workspacePaths, operationName, cancellationToken, repo => repoOperation(repo, gitRootPath, workspacePaths));
#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: {logLabel} {result.Count} files");
#endif
            return result;
        }

        private async Task PeriodicScanAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var didCleanup = ReviewCacheCleanup.CleanupCaches(_gitRootPath);
                var files = await GetAllChangedFilesAsync(_gitRootPath, _workspacePaths, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (didCleanup)
                {
                    files ??= new HashSet<string>();
                    files.Add("~~cleanup~~");
                }

                if (files == null || files.Count == 0)
                {
                    return;
                }

#if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeLister: Periodic scan detected {files.Count} files");
#endif
                FilesDetected?.Invoke(this, files);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeLister: Error during periodic scan: {ex.Message}");
            }
        }

        private bool IsValidGitRoot(string gitRootPath)
        {
            var isValid = !string.IsNullOrEmpty(gitRootPath) && Directory.Exists(gitRootPath);
            if (!isValid)
            {
#if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeLister: Invalid git root path '{gitRootPath}'");
#endif
            }

            return isValid;
        }

        private HashSet<string> GetChangedFilesVsMergeBase(Repository repo, string gitRootPath, IReadOnlyCollection<string> workspacePaths)
        {
            var currentBranch = repo.Head?.FriendlyName ?? "unknown";
#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: Getting changed files vs merge base on branch '{currentBranch}'");
#endif

            var mergeBase = _mergeBaseFinder.GetMergeBaseCommit(repo);
            if (mergeBase == null)
            {
                if (repo.Head != null && !_mergeBaseFinder.IsMainBranch(repo.Head.FriendlyName))
                {
                    _logger?.Warn("GitChangeLister: On non-main branch but can't determine merge-base");
                }

                return new HashSet<string>();
            }

            if (repo.Head?.Tip == null)
            {
                return new HashSet<string>();
            }

            return GetCommittedChanges(repo, mergeBase, gitRootPath, workspacePaths);
        }

        private HashSet<string> GetCommittedChanges(
            Repository repo,
            Commit mergeBase,
            string gitRootPath,
            IReadOnlyCollection<string> workspacePaths)
        {
            var diff = repo.Diff.Compare<TreeChanges>(mergeBase.Tree, repo.Head.Tip.Tree);
            var changedFiles = new HashSet<string>();

            foreach (var change in diff)
            {
                var relativePath = change.Path;
                var fullPath = Path.Combine(gitRootPath, relativePath);
                if (!File.Exists(fullPath) || _gitService.IsFileIgnored(fullPath))
                {
                    continue;
                }

                if (GitPathHelper.IsFileInWorkspace(relativePath, gitRootPath, workspacePaths))
                {
                    changedFiles.Add(relativePath);
                }
            }

#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: GetCommittedChanges found {changedFiles.Count} committed changes");
#endif
            return changedFiles;
        }

        private bool ShouldSkipStatusItem(StatusEntry item)
        {
            return item.State == FileStatus.Unaltered ||
                   item.State == FileStatus.Ignored ||
                   item.State.HasFlag(FileStatus.DeletedFromWorkdir) ||
                   item.State.HasFlag(FileStatus.DeletedFromIndex);
        }

        private bool ShouldReviewFile(string absolutePath)
        {
            return _supportedFileChecker.IsSupported(absolutePath);
        }
    }
}
