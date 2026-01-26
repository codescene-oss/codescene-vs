using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    public class GitChangeDetector
    {
        private readonly ILogger _logger;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private Dictionary<string, List<string>> _mainBranchCandidatesCache;

        public GitChangeDetector(ILogger logger, ISupportedFileChecker supportedFileChecker)
        {
            _logger = logger;
            _supportedFileChecker = supportedFileChecker;
        }

        public virtual async Task<List<string>> GetChangedFilesVsBaselineAsync(string gitRootPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(gitRootPath) || !Directory.Exists(gitRootPath))
                    {
                        return new List<string>();
                    }

                    var repoPath = Repository.Discover(gitRootPath);
                    if (string.IsNullOrEmpty(repoPath))
                    {
                        return new List<string>();
                    }

                    using (var repo = new Repository(repoPath))
                    {
                        return GetChangedFilesFromRepository(repo, gitRootPath, savedFilesTracker, openFilesObserver);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error getting changed files: {ex.Message}");
                    return new List<string>();
                }
            });
        }

        private List<string> GetChangedFilesFromRepository(Repository repo, string gitRootPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            var baseCommit = GetMergeBaseCommit(repo);
            if (baseCommit == null)
            {
                _logger?.Debug("GitChangeObserver: No merge base commit found, using working directory changes only");
            }

            var filesToExclude = BuildExclusionSet(savedFilesTracker, openFilesObserver);
            var committedChanges = GetCommittedChanges(repo, baseCommit, gitRootPath);
            var statusChanges = GetStatusChanges(repo, filesToExclude, gitRootPath);

            var changedFiles = new List<string>();
            changedFiles.AddRange(committedChanges);
            changedFiles.AddRange(statusChanges);

            return changedFiles.Distinct().ToList();
        }

        private HashSet<string> BuildExclusionSet(ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            var exclusionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddFilesToExclusionSet(exclusionSet, savedFilesTracker?.GetSavedFiles());
            AddFilesToExclusionSet(exclusionSet, openFilesObserver?.GetAllVisibleFileNames());

            return exclusionSet;
        }

        private void AddFilesToExclusionSet(HashSet<string> exclusionSet, IEnumerable<string> files)
        {
            if (files == null)
            {
                return;
            }

            foreach (var file in files)
            {
                exclusionSet.Add(file);
            }
        }

        private Commit GetMergeBaseCommit(Repository repo)
        {
            try
            {
                var currentBranch = repo.Head;
                if (!IsValidBranch(currentBranch))
                {
                    return null;
                }

                var mainBranchCandidates = GetMainBranchCandidates(repo);
                if (mainBranchCandidates.Count == 0)
                {
                    _logger?.Debug("GitChangeObserver: No main branch candidates found");
                    return null;
                }

                foreach (var candidateName in mainBranchCandidates)
                {
                    var mergeBase = TryFindMergeBase(repo, currentBranch, candidateName);
                    if (mergeBase != null)
                    {
                        return mergeBase;
                    }
                }

                _logger?.Debug("GitChangeObserver: No merge base found with any main branch candidate");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Could not determine merge base: {ex.Message}");
                return null;
            }
        }

        private Commit TryFindMergeBase(Repository repo, Branch currentBranch, string candidateName)
        {
            var mainBranch = repo.Branches[candidateName];
            if (!IsValidBranch(mainBranch))
            {
                return null;
            }

            if (IsOnMainBranch(currentBranch, mainBranch))
            {
                return null;
            }

            try
            {
                var mergeBase = repo.ObjectDatabase.FindMergeBase(currentBranch.Tip, mainBranch.Tip);
                if (mergeBase != null)
                {
                    _logger?.Debug($"GitChangeObserver: Found merge base using branch '{candidateName}'");
                }
                return mergeBase;
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Could not find merge base with '{candidateName}': {ex.Message}");
                return null;
            }
        }

        private bool IsValidBranch(Branch branch)
        {
            return branch != null && branch.Tip != null;
        }

        private List<string> GetMainBranchCandidates(Repository repo)
        {
            var gitRoot = repo.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(gitRoot))
            {
                return new List<string>();
            }

            if (_mainBranchCandidatesCache != null &&
                _mainBranchCandidatesCache.TryGetValue(gitRoot, out var cached))
            {
                return cached;
            }

            var possibleMainBranches = new[] { "main", "master", "develop", "trunk", "dev" };

            var localBranches = repo.Branches
                .Where(b => !b.IsRemote)
                .Select(b => b.FriendlyName)
                .ToList();

            var candidates = possibleMainBranches
                .Where(name => localBranches.Contains(name))
                .ToList();

            if (_mainBranchCandidatesCache == null)
            {
                _mainBranchCandidatesCache = new Dictionary<string, List<string>>();
            }
            _mainBranchCandidatesCache[gitRoot] = candidates;

            return candidates;
        }

        private bool IsOnMainBranch(Branch currentBranch, Branch mainBranch)
        {
            return currentBranch.FriendlyName == mainBranch.FriendlyName;
        }

        private List<string> GetCommittedChanges(Repository repo, Commit baseCommit, string gitRootPath)
        {
            var changes = new List<string>();

            try
            {
                if (baseCommit == null || repo.Head?.Tip == null)
                {
                    return changes;
                }

                var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, repo.Head.Tip.Tree);

                foreach (var change in diff)
                {
                    var relativePath = change.Path;
                    var fullPath = Path.Combine(gitRootPath, relativePath);

                    if (_supportedFileChecker.IsSupported(fullPath))
                    {
                        changes.Add(relativePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Error getting committed changes: {ex.Message}");
            }

            return changes;
        }

        private List<string> GetStatusChanges(Repository repo, HashSet<string> filesToExclude, string gitRootPath)
        {
            var changes = new List<string>();

            try
            {
                var status = repo.RetrieveStatus();

                foreach (var item in status)
                {
                    if (ShouldIncludeStatusItem(item, filesToExclude, gitRootPath))
                    {
                        changes.Add(item.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Error getting status changes: {ex.Message}");
            }

            return changes;
        }

        private bool ShouldIncludeStatusItem(StatusEntry item, HashSet<string> filesToExclude, string gitRootPath)
        {
            if (item.State == FileStatus.Unaltered || item.State == FileStatus.Ignored)
            {
                return false;
            }

            var fullPath = Path.Combine(gitRootPath, item.FilePath);

            if (filesToExclude.Contains(fullPath))
            {
                return false;
            }

            return _supportedFileChecker.IsSupported(fullPath);
        }
    }
}
