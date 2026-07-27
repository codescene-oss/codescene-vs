// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
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
        private readonly int _basePollingInterval;
        private readonly int _pollingInterval; // Calculated based on core count, may differ from base.
        private readonly ISavedFilesTracker _savedFilesTracker;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly ILogger _logger;
        private readonly IGitService _gitService;
        private readonly IIdeActivityTracker _ideActivityTracker;
        private readonly UntrackedFileProcessor _untrackedFileProcessor;
        private readonly MergeBaseFinder _mergeBaseFinder;

        private string _gitRootPath;
        private IReadOnlyCollection<string> _workspacePaths;
        private DroppingScheduledExecutor _scheduledExecutor;
        private ConcurrentDictionary<string, string> _loggedNoMergeBaseWarnKeysByRepo;
        private bool _disposed = false;

        public GitChangeLister(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger,
            IGitService gitService,
            int? pollingInterval = null,
            IIdeActivityTracker ideActivityTracker = null)
        {
            _savedFilesTracker = savedFilesTracker ?? throw new ArgumentNullException(nameof(savedFilesTracker));
            _supportedFileChecker = supportedFileChecker ?? throw new ArgumentNullException(nameof(supportedFileChecker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _ideActivityTracker = ideActivityTracker;
            _untrackedFileProcessor = new UntrackedFileProcessor(logger);
            _mergeBaseFinder = new MergeBaseFinder(logger);
            if (pollingInterval.HasValue && pollingInterval.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pollingInterval), pollingInterval.Value, "Polling interval must be greater than 0.");
            }

            _pollingInterval = pollingInterval ?? CalculatePollingInterval();
            _basePollingInterval = _pollingInterval;
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

                var (trackedCandidates, untrackedCandidates) = CategorizeStatusItems(status, gitRootPath, workspacePaths);

                var nonIgnoredTracked = _gitService.FilterIgnoredFiles(trackedCandidates);
                changedFiles.UnionWith(nonIgnoredTracked);

                var untrackedAbsolutePaths = untrackedCandidates.Select(x => x.AbsolutePath);
                var nonIgnoredUntracked = _gitService.FilterIgnoredFiles(untrackedAbsolutePaths);
                AddNonIgnoredUntrackedFiles(untrackedCandidates, nonIgnoredUntracked, untrackedByDirectory);

                _untrackedFileProcessor.ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);
#if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeLister: CollectFilesFromRepoState collected {changedFiles.Count} files from repo state");
#endif
            }
            catch (Exception ex)
            {
                _logger?.Error("GitChangeLister: Error collecting files from repo state", ex);
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
                _logger?.Error("GitChangeLister: Error collecting files from git diff", ex);
                return new HashSet<string>();
            }
        }

        protected HashSet<string> ConvertAndFilterPaths(IEnumerable<string> relativePaths, string gitRootPath)
        {
            var candidates = new List<string>();
            foreach (var relativePath in relativePaths)
            {
                var absolutePath = GitPathHelper.ConvertToAbsolutePath(relativePath, gitRootPath);
                if (File.Exists(absolutePath) && ShouldReviewFile(absolutePath))
                {
                    candidates.Add(absolutePath);
                }
            }

            return _gitService.FilterIgnoredFiles(candidates);
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
                    _logger?.Error($"GitChangeLister: Error {operationName}", ex);
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
            var startTime = DateTime.UtcNow;
            try
            {
                if (!ShouldRunPeriodicScan())
                {
                    return;
                }

                _logger?.Info("Starting scheduled git change review");
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
                _logger?.Error("GitChangeLister: Error during periodic scan", ex);
            }
            finally
            {
                AdjustIntervalIfNeeded(startTime);
            }
        }

        private void AdjustIntervalIfNeeded(DateTime startTime)
        {
            var elapsedSeconds = (int)Math.Ceiling((DateTime.UtcNow - startTime).TotalSeconds);
            _logger?.Debug($"Scheduled git change review completed in {elapsedSeconds}s");

            var executor = _scheduledExecutor;
            if (elapsedSeconds > _basePollingInterval && executor != null)
            {
                var newPeriodSeconds = (_basePollingInterval * 2) + elapsedSeconds;
                var currentPeriodSeconds = (int)executor.GetInterval().TotalSeconds;
                if (newPeriodSeconds > currentPeriodSeconds)
                {
                    executor.SetInterval(TimeSpan.FromSeconds(newPeriodSeconds));
                    _logger?.Info($"Git change review took {elapsedSeconds}s, increased period to {newPeriodSeconds}s");
                }
            }
        }

        private bool ShouldRunPeriodicScan()
        {
            if (_ideActivityTracker != null && !_ideActivityTracker.IsIdeWindowActive())
            {
                return false;
            }

            if (DeltaJobTracker.IsAnalysisRunning)
            {
                _logger?.Info("Skipping scheduled git change review: analysis in progress");
                return false;
            }

            return true;
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
                LogNoMergeBaseWarnOnce(repo);
                return new HashSet<string>();
            }

            ClearNoMergeBaseWarnLogState(repo.Info.WorkingDirectory);

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
            var candidates = CollectDiffCandidates(diff, gitRootPath, workspacePaths);

            var candidatePaths = candidates.Select(c => c.FullPath);
            var nonIgnored = _gitService.FilterIgnoredFiles(candidatePaths);

            var changedFiles = new HashSet<string>(
                candidates.Where(c => nonIgnored.Contains(c.FullPath)).Select(c => c.RelativePath));

#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeLister: GetCommittedChanges found {changedFiles.Count} committed changes");
#endif
            return changedFiles;
        }

        private List<(string RelativePath, string FullPath)> CollectDiffCandidates(
            TreeChanges diff,
            string gitRootPath,
            IReadOnlyCollection<string> workspacePaths)
        {
            var candidates = new List<(string RelativePath, string FullPath)>();

            foreach (var change in diff)
            {
                var relativePath = change.Path;
                var fullPath = Path.Combine(gitRootPath, relativePath);

                if (!File.Exists(fullPath))
                {
                    continue;
                }

                if (GitPathHelper.IsFileInWorkspace(relativePath, gitRootPath, workspacePaths))
                {
                    candidates.Add((relativePath, fullPath));
                }
            }

            return candidates;
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

        private void LogNoMergeBaseWarnOnce(Repository repo)
        {
            if (repo?.Head == null || _mergeBaseFinder.IsMainBranch(repo, repo.Head.FriendlyName))
            {
                return;
            }

            if (!TryMarkNoMergeBaseWarnLogged(repo))
            {
                return;
            }

            _logger?.Warn("GitChangeLister: On non-main branch but can't determine merge-base");
        }

        private void ClearNoMergeBaseWarnLogState(string gitRootPath = null)
        {
            if (_loggedNoMergeBaseWarnKeysByRepo == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(gitRootPath))
            {
                _loggedNoMergeBaseWarnKeysByRepo = null;
                return;
            }

            var key = gitRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _loggedNoMergeBaseWarnKeysByRepo.TryRemove(key, out _);
            if (_loggedNoMergeBaseWarnKeysByRepo.Count == 0)
            {
                _loggedNoMergeBaseWarnKeysByRepo = null;
            }
        }

        private bool TryMarkNoMergeBaseWarnLogged(Repository repo)
        {
            var gitRoot = repo?.Info?.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(gitRoot) || repo?.Head?.Tip == null)
            {
                return false;
            }

            var defaultBranch = MainBranchNames.GetDefaultBranch(repo) ?? string.Empty;
            var stateKey = $"{repo.Head.Tip.Sha}|{repo.Head.FriendlyName}|{defaultBranch}";

            if (_loggedNoMergeBaseWarnKeysByRepo == null)
            {
                _loggedNoMergeBaseWarnKeysByRepo = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (_loggedNoMergeBaseWarnKeysByRepo.TryGetValue(gitRoot, out var lastStateKey) &&
                string.Equals(lastStateKey, stateKey, StringComparison.Ordinal))
            {
                return false;
            }

            _loggedNoMergeBaseWarnKeysByRepo[gitRoot] = stateKey;
            return true;
        }

        private (List<string> Tracked, List<(string RelativePath, string AbsolutePath)> Untracked) CategorizeStatusItems(
            RepositoryStatus status,
            string gitRootPath,
            IReadOnlyCollection<string> workspacePaths)
        {
            var trackedCandidates = new List<string>();
            var untrackedCandidates = new List<(string RelativePath, string AbsolutePath)>();

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

                if (item.State == FileStatus.NewInWorkdir)
                {
                    untrackedCandidates.Add((item.FilePath, absolutePath));
                }
                else
                {
                    trackedCandidates.Add(absolutePath);
                }
            }

            return (trackedCandidates, untrackedCandidates);
        }

        private void AddNonIgnoredUntrackedFiles(
            List<(string RelativePath, string AbsolutePath)> untrackedCandidates,
            HashSet<string> nonIgnoredUntracked,
            Dictionary<string, List<string>> untrackedByDirectory)
        {
            foreach (var candidate in untrackedCandidates)
            {
                if (nonIgnoredUntracked.Contains(candidate.AbsolutePath))
                {
                    _untrackedFileProcessor.AddUntrackedFileToDirectory(candidate.RelativePath, candidate.AbsolutePath, untrackedByDirectory);
                }
            }
        }
    }
}
