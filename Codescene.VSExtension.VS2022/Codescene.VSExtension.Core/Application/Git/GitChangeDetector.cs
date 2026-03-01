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
using Codescene.VSExtension.Core.Models.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitChangeDetector
    {
        private readonly ILogger _logger;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly IGitService _gitService;
        private Dictionary<string, List<string>> _mainBranchCandidatesCache;

        public GitChangeDetector(ILogger logger, ISupportedFileChecker supportedFileChecker, IGitService gitService)
        {
            _logger = logger;
            _supportedFileChecker = supportedFileChecker;
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        }

        public virtual async Task<List<string>> GetChangedFilesVsBaselineAsync(string gitRootPath, string workspacePath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
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

                    var effectiveWorkspacePath = string.IsNullOrEmpty(workspacePath) ? gitRootPath : workspacePath;

                    using (var repo = new Repository(repoPath))
                    {
                        var context = new ChangeDetectionContext(gitRootPath, effectiveWorkspacePath, savedFilesTracker, openFilesObserver);
                        var changedFiles = GetChangedFilesFromRepository(repo, context);
                        #if FEATURE_INITIAL_GIT_OBSERVER
                        _logger?.Info($">>> GitChangeDetector: Found {changedFiles.Count} changed files vs baseline");
                        #endif
                        return changedFiles;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error getting changed files: {ex.Message}");
                    return new List<string>();
                }
            });
        }

        public virtual List<string> GetMainBranchCandidates(Repository repo)
        {
            var gitRoot = repo.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(gitRoot))
            {
                return new List<string>();
            }

            if (_mainBranchCandidatesCache != null &&
                _mainBranchCandidatesCache.TryGetValue(gitRoot, out var cached))
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeDetector: Returning cached main branch candidates ({cached.Count} candidates)");
                #endif
                return cached;
            }

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info(">>> GitChangeDetector: Computing main branch candidates");
            #endif

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

        protected virtual List<string> GetChangedFilesFromRepository(Repository repo, ChangeDetectionContext context)
        {
            var currentBranch = repo.Head?.FriendlyName ?? "unknown";
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeDetector: Getting changed files from repository on branch '{currentBranch}'");
            #endif

            var baseCommit = GetMergeBaseCommit(repo);
            if (baseCommit == null)
            {
                _logger?.Debug("GitChangeObserver: No merge base commit found, using working directory changes only");
            }

            var filesToExclude = BuildExclusionSet(context.SavedFilesTracker, context.OpenFilesObserver);
            var committedChanges = GetCommittedChanges(repo, baseCommit, context.GitRootPath, context.WorkspacePath);
            var statusChanges = GetStatusChanges(repo, filesToExclude, context.GitRootPath, context.WorkspacePath);

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeDetector: Found {committedChanges.Count} committed changes and {statusChanges.Count} status changes");
            #endif

            var changedFiles = new List<string>();
            changedFiles.AddRange(committedChanges);
            changedFiles.AddRange(statusChanges);

            return changedFiles.Distinct().ToList();
        }

        protected virtual Commit GetMergeBaseCommit(Repository repo)
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

        protected virtual Commit TryFindMergeBase(Repository repo, Branch currentBranch, string candidateName)
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

        protected virtual List<string> GetCommittedChanges(Repository repo, Commit baseCommit, string gitRootPath, string workspacePath)
        {
            var changes = new List<string>();

            try
            {
                if (baseCommit == null || repo.Head?.Tip == null)
                {
                    return changes;
                }

                var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, repo.Head.Tip.Tree);
                var relativePaths = diff.Where(c => ShouldIncludeCommittedChange(c.Path, gitRootPath)).Select(c => c.Path).ToList();
                AddWorkspacePathsFromRelativePaths(relativePaths, gitRootPath, workspacePath, changes);

                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeDetector: Collected {changes.Count} committed changes");
                #endif
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Error getting committed changes: {ex.Message}");
            }

            return changes;
        }

        protected virtual List<string> GetStatusChanges(Repository repo, HashSet<string> filesToExclude, string gitRootPath, string workspacePath)
        {
            var changes = new List<string>();

            try
            {
                var status = repo.RetrieveStatus();
                var relativePaths = status.Where(item => ShouldIncludeStatusItem(item, filesToExclude, gitRootPath)).Select(item => item.FilePath).ToList();
                AddWorkspacePathsFromRelativePaths(relativePaths, gitRootPath, workspacePath, changes);

                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeDetector: Collected {changes.Count} status changes");
                #endif
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Error getting status changes: {ex.Message}");
            }

            return changes;
        }

        private void AddWorkspacePathsFromRelativePaths(IEnumerable<string> relativePaths, string gitRootPath, string workspacePath, List<string> output)
        {
            foreach (var path in relativePaths)
            {
                if (IsFileInWorkspace(path, gitRootPath, workspacePath))
                {
                    output.Add(ConvertGitPathToWorkspacePath(path, gitRootPath, workspacePath));
                }
            }
        }

        private bool ShouldIncludeCommittedChange(string relativePath, string gitRootPath)
        {
            var fullPath = Path.Combine(gitRootPath, relativePath);
            if (!File.Exists(fullPath) || _gitService.IsFileIgnored(fullPath))
            {
                return false;
            }

            return _supportedFileChecker.IsSupported(fullPath);
        }

        private HashSet<string> BuildExclusionSet(ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            var exclusionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddFilesToExclusionSet(exclusionSet, savedFilesTracker?.GetSavedFiles());
            AddFilesToExclusionSet(exclusionSet, openFilesObserver?.GetAllVisibleFileNames());

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeDetector: Built exclusion set with {exclusionSet.Count} files");
            #endif
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

        private bool IsValidBranch(Branch branch)
        {
            return branch != null && branch.Tip != null;
        }

        private bool IsOnMainBranch(Branch currentBranch, Branch mainBranch)
        {
            return currentBranch.FriendlyName == mainBranch.FriendlyName;
        }

        private bool ShouldIncludeStatusItem(StatusEntry item, HashSet<string> filesToExclude, string gitRootPath)
        {
            if (item.State == FileStatus.Unaltered || item.State == FileStatus.Ignored)
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeDetector: File excluded - unaltered or ignored: {item.FilePath}");
                #endif
                return false;
            }

            var fullPath = Path.Combine(gitRootPath, item.FilePath);

            if (_gitService.IsFileIgnored(fullPath))
            {
                return false;
            }

            if (filesToExclude.Contains(fullPath))
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeDetector: File excluded - in exclusion set: {item.FilePath}");
                #endif
                return false;
            }

            var isSupported = _supportedFileChecker.IsSupported(fullPath);
            if (!isSupported)
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> GitChangeDetector: File excluded - unsupported file type: {item.FilePath}");
                #endif
            }

            return isSupported;
        }

        private bool IsFileInWorkspace(string gitRelativePath, string gitRootPath, string workspacePath)
        {
            try
            {
                var absolutePath = Path.GetFullPath(Path.Combine(gitRootPath, gitRelativePath));
                if (!File.Exists(absolutePath))
                {
                    return false;
                }

                var normalizedWorkspace = Path.GetFullPath(workspacePath);
                var workspacePrefix = normalizedWorkspace.EndsWith(Path.DirectorySeparatorChar.ToString())
                    ? normalizedWorkspace
                    : normalizedWorkspace + Path.DirectorySeparatorChar;
                return absolutePath.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase) ||
                       absolutePath.Equals(normalizedWorkspace, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string ConvertGitPathToWorkspacePath(string gitRelativePath, string gitRootPath, string workspacePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(gitRootPath, gitRelativePath));
            var relativeToWorkspace = PathUtilities.GetRelativePath(Path.GetFullPath(workspacePath), absolutePath);
            return relativeToWorkspace.Replace('\\', '/');
        }
    }
}
