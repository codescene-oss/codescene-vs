// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitChangeLister : IGitChangeLister
    {
        private const int MaxUntrackedFilesPerLocation = 5;

        private readonly ISavedFilesTracker _savedFilesTracker;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly ILogger _logger;

        public GitChangeLister(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger)
        {
            _savedFilesTracker = savedFilesTracker ?? throw new ArgumentNullException(nameof(savedFilesTracker));
            _supportedFileChecker = supportedFileChecker ?? throw new ArgumentNullException(nameof(supportedFileChecker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(gitRootPath) || !Directory.Exists(gitRootPath))
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
                        var statusFiles = CollectFilesFromRepoState(repo, gitRootPath, workspacePath);
                        var diffFiles = CollectFilesFromGitDiff(repo, gitRootPath, workspacePath);

                        var allFiles = new HashSet<string>(statusFiles);
                        allFiles.UnionWith(diffFiles);

                        return allFiles;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeLister: Error getting all changed files: {ex.Message}");
                    return new HashSet<string>();
                }
            });
        }

        public virtual async Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath)
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
                        return GetChangedFilesFromMergeBase(repo, gitRootPath, workspacePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeLister: Error getting changed files vs merge base: {ex.Message}");
                    return new HashSet<string>();
                }
            });
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
                        AddUntrackedFileToDirectory(item.FilePath, absolutePath, untrackedByDirectory);
                    }
                    else
                    {
                        changedFiles.Add(absolutePath);
                    }
                }

                ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeLister: Error collecting files from repo state: {ex.Message}");
            }

            return changedFiles;
        }

        protected virtual HashSet<string> CollectFilesFromGitDiff(Repository repo, string gitRootPath, string workspacePath)
        {
            var changedFiles = new HashSet<string>();

            try
            {
                var mergeBase = GetMergeBaseCommit(repo);
                if (mergeBase == null || repo.Head?.Tip == null)
                {
                    return changedFiles;
                }

                var diff = repo.Diff.Compare<TreeChanges>(mergeBase.Tree, repo.Head.Tip.Tree);
                ProcessDiffChanges(diff, gitRootPath, workspacePath, changedFiles);
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeLister: Error collecting files from git diff: {ex.Message}");
            }

            return changedFiles;
        }

        protected virtual Commit GetMergeBaseCommit(Repository repo)
        {
            try
            {
                var currentBranch = repo.Head;
                if (currentBranch == null || currentBranch.Tip == null)
                {
                    return null;
                }

                var mainBranchCandidates = new[] { "main", "master", "develop", "trunk", "dev" };

                foreach (var candidateName in mainBranchCandidates)
                {
                    var mergeBase = TryFindMergeBaseWithBranch(repo, currentBranch, candidateName);
                    if (mergeBase != null)
                    {
                        return mergeBase;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeLister: Could not determine merge base: {ex.Message}");
                return null;
            }
        }

        private bool IsValidGitRoot(string gitRootPath)
        {
            return !string.IsNullOrEmpty(gitRootPath) && Directory.Exists(gitRootPath);
        }

        private HashSet<string> GetChangedFilesFromMergeBase(Repository repo, string gitRootPath, string workspacePath)
        {
            var mergeBase = GetMergeBaseCommit(repo);
            if (mergeBase == null)
            {
                if (repo.Head != null && !IsMainBranch(repo.Head.FriendlyName))
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

        private void AddUntrackedFileToDirectory(
            string relativePath,
            string absolutePath,
            Dictionary<string, List<string>> untrackedByDirectory)
        {
            var directory = string.IsNullOrEmpty(Path.GetDirectoryName(relativePath)) ? "__root__" : Path.GetDirectoryName(relativePath);

            if (!untrackedByDirectory.ContainsKey(directory))
            {
                untrackedByDirectory[directory] = new List<string>();
            }

            untrackedByDirectory[directory].Add(absolutePath);
        }

        private void ProcessUntrackedDirectories(
            Dictionary<string, List<string>> untrackedByDirectory,
            HashSet<string> savedFiles,
            HashSet<string> changedFiles)
        {
            foreach (var kvp in untrackedByDirectory)
            {
                var files = kvp.Value;
                var filesToAdd = files.Count > MaxUntrackedFilesPerLocation
                    ? files.Where(f => savedFiles.Contains(f))
                    : files;

                changedFiles.UnionWith(filesToAdd);
            }
        }

        private void ProcessDiffChanges(
            TreeChanges diff,
            string gitRootPath,
            string workspacePath,
            HashSet<string> changedFiles)
        {
            foreach (var change in diff)
            {
                var absolutePath = ConvertToAbsolutePath(change.Path, gitRootPath);

                if (!IsFileInWorkspace(change.Path, gitRootPath, workspacePath))
                {
                    continue;
                }

                if (!ShouldReviewFile(absolutePath))
                {
                    continue;
                }

                changedFiles.Add(absolutePath);
            }
        }

        private Commit TryFindMergeBaseWithBranch(Repository repo, Branch currentBranch, string candidateName)
        {
            var mainBranch = repo.Branches[candidateName];
            if (!IsValidBranchForMergeBase(mainBranch, currentBranch))
            {
                return null;
            }

            try
            {
                var mergeBase = repo.ObjectDatabase.FindMergeBase(currentBranch.Tip, mainBranch.Tip);
                if (mergeBase != null)
                {
                    _logger?.Debug($"GitChangeLister: Found merge base using branch '{candidateName}'");
                    return mergeBase;
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeLister: Could not find merge base with '{candidateName}': {ex.Message}");
            }

            return null;
        }

        private bool IsValidBranchForMergeBase(Branch mainBranch, Branch currentBranch)
        {
            return mainBranch != null &&
                   mainBranch.Tip != null &&
                   currentBranch.FriendlyName != mainBranch.FriendlyName;
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

        private bool IsMainBranch(string branchName)
        {
            return !string.IsNullOrEmpty(branchName) &&
                   new[] { "main", "master", "develop", "trunk", "dev" }.Contains(branchName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
