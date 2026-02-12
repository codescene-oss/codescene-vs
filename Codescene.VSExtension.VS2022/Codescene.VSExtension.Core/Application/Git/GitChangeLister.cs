// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitChangeLister : IGitChangeLister, IDisposable
    {
        private readonly ISavedFilesTracker _savedFilesTracker;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly ILogger _logger;
        private readonly UntrackedFileProcessor _untrackedFileProcessor = new UntrackedFileProcessor();
        private readonly MergeBaseFinder _mergeBaseFinder;

        private string _gitRootPath;
        private string _workspacePath;
        private DroppingScheduledExecutor _scheduledExecutor;
        private bool _disposed = false;

        public GitChangeLister(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger)
        {
            _savedFilesTracker = savedFilesTracker ?? throw new ArgumentNullException(nameof(savedFilesTracker));
            _supportedFileChecker = supportedFileChecker ?? throw new ArgumentNullException(nameof(supportedFileChecker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mergeBaseFinder = new MergeBaseFinder(logger);
        }

        public event EventHandler<HashSet<string>> FilesDetected;

        public virtual async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath)
        {
            return await ExecuteGitOperationAsync(gitRootPath, workspacePath, "getting all changed files", repo =>
            {
                var statusFiles = CollectFilesFromRepoState(repo, gitRootPath, workspacePath);
                var diffFiles = CollectFilesFromGitDiff(repo, gitRootPath, workspacePath);
                var allFiles = new HashSet<string>(statusFiles);
                allFiles.UnionWith(diffFiles);
                return allFiles;
            });
        }

        public virtual async Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath)
        {
            return await ExecuteGitOperationAsync(gitRootPath, workspacePath, "getting changed files vs merge base", repo =>
            {
                return GetChangedFilesFromMergeBase(repo, gitRootPath, workspacePath);
            });
        }

        public void Initialize(string gitRootPath, string workspacePath)
        {
            _gitRootPath = gitRootPath;
            _workspacePath = workspacePath;
        }

        public void StartPeriodicScanning()
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
                TimeSpan.FromSeconds(9),
                _logger);

            _scheduledExecutor.Start();
        }

        public void StopPeriodicScanning()
        {
            _scheduledExecutor?.Stop();
        }

        public virtual async Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, string workspacePath)
        {
            return await ExecuteGitOperationAsync(gitRootPath, workspacePath, "collecting files from repo state", repo =>
            {
                return CollectFilesFromRepoState(repo, gitRootPath, workspacePath);
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            StopPeriodicScanning();
            _scheduledExecutor?.Dispose();
            _scheduledExecutor = null;
            _disposed = true;
        }

        protected virtual HashSet<string> CollectFilesFromRepoState(Repository repo, string gitRootPath, string workspacePath)
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

                    var absolutePath = ConvertToAbsolutePath(item.FilePath, gitRootPath);

                    if (!IsFileInWorkspace(item.FilePath, gitRootPath, workspacePath) || !ShouldReviewFile(absolutePath))
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
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeLister: Error collecting files from repo state: {ex.Message}");
            }

            return changedFiles;
        }

        protected virtual HashSet<string> CollectFilesFromGitDiff(Repository repo, string gitRootPath, string workspacePath)
        {
            try
            {
                var relativePaths = GetChangedFilesFromMergeBase(repo, gitRootPath, workspacePath);
                return ConvertAndFilterPaths(relativePaths, gitRootPath);
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
                var absolutePath = ConvertToAbsolutePath(relativePath, gitRootPath);
                if (ShouldReviewFile(absolutePath))
                {
                    result.Add(absolutePath);
                }
            }

            return result;
        }

        private async Task<HashSet<string>> ExecuteGitOperationAsync(
            string gitRootPath,
            string workspacePath,
            string operationName,
            Func<Repository, HashSet<string>> operation)
        {
            return await Task.Run(() =>
            {
                try
                {
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
                        return operation(repo);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeLister: Error {operationName}: {ex.Message}");
                    return new HashSet<string>();
                }
            });
        }

        private async Task PeriodicScanAsync()
        {
            try
            {
                var files = await CollectFilesFromRepoStateAsync(_gitRootPath, _workspacePath);
                if (files != null && files.Count > 0)
                {
                    FilesDetected?.Invoke(this, files);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeLister: Error during periodic scan: {ex.Message}");
            }
        }

        private bool IsValidGitRoot(string gitRootPath)
        {
            return !string.IsNullOrEmpty(gitRootPath) && Directory.Exists(gitRootPath);
        }

        private HashSet<string> GetChangedFilesFromMergeBase(Repository repo, string gitRootPath, string workspacePath)
        {
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

            return CollectChangedFilesFromDiff(repo, mergeBase, gitRootPath, workspacePath);
        }

        private HashSet<string> CollectChangedFilesFromDiff(
            Repository repo,
            Commit mergeBase,
            string gitRootPath,
            string workspacePath)
        {
            var diff = repo.Diff.Compare<TreeChanges>(mergeBase.Tree, repo.Head.Tip.Tree);
            var changedFiles = new HashSet<string>();

            foreach (var change in diff)
            {
                var relativePath = change.Path;
                if (IsFileInWorkspace(relativePath, gitRootPath, workspacePath))
                {
                    changedFiles.Add(relativePath);
                }
            }

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

        private bool IsFileInWorkspace(string relativePath, string gitRootPath, string workspacePath)
        {
            try
            {
                var absolutePath = Path.Combine(gitRootPath, relativePath);
                var normalizedAbsolute = Path.GetFullPath(absolutePath);
                var normalizedWorkspace = Path.GetFullPath(workspacePath);

                if (!File.Exists(normalizedAbsolute))
                {
                    return false;
                }

                return normalizedAbsolute.StartsWith(normalizedWorkspace, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string ConvertToAbsolutePath(string relativePath, string basePath)
        {
            try
            {
                return Path.GetFullPath(Path.Combine(basePath, relativePath));
            }
            catch
            {
                return Path.Combine(basePath, relativePath);
            }
        }
    }
}
