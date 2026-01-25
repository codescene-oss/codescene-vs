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
    internal class GitChangeDetector
    {
        private readonly ILogger _logger;
        private readonly ISupportedFileChecker _supportedFileChecker;

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
                    var changedFiles = new List<string>();

                    if (string.IsNullOrEmpty(gitRootPath) || !Directory.Exists(gitRootPath))
                    {
                        return changedFiles;
                    }

                    var repoPath = Repository.Discover(gitRootPath);
                    if (string.IsNullOrEmpty(repoPath))
                    {
                        return changedFiles;
                    }

                    using (var repo = new Repository(repoPath))
                    {
                        var baseCommit = GetMergeBaseCommit(repo);

                    if (baseCommit == null)
                    {
                        _logger?.Debug("GitChangeObserver: No merge base commit found, using working directory changes only");
                    }

                    var filesToExcludeFromHeuristic = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (savedFilesTracker != null)
                    {
                        foreach (var file in savedFilesTracker.GetSavedFiles())
                        {
                            filesToExcludeFromHeuristic.Add(file);
                        }
                    }
                    if (openFilesObserver != null)
                    {
                        foreach (var file in openFilesObserver.GetAllVisibleFileNames())
                        {
                            filesToExcludeFromHeuristic.Add(file);
                        }
                    }

                        var committedChanges = GetCommittedChanges(repo, baseCommit, gitRootPath);
                        var statusChanges = GetStatusChanges(repo, filesToExcludeFromHeuristic, gitRootPath);

                        changedFiles.AddRange(committedChanges);
                        changedFiles.AddRange(statusChanges);

                        return changedFiles.Distinct().ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"GitChangeObserver: Error getting changed files: {ex.Message}");
                    return new List<string>();
                }
            });
        }

        private Commit GetMergeBaseCommit(Repository repo)
        {
            try
            {
                if (repo.Head == null || repo.Head.Tip == null)
                {
                    return null;
                }

                var currentBranch = repo.Head;

                var mainBranch = repo.Branches["main"] ?? repo.Branches["master"] ?? repo.Branches["origin/main"] ?? repo.Branches["origin/master"];

                if (mainBranch == null || mainBranch.Tip == null)
                {
                    return null;
                }

                if (currentBranch.FriendlyName == mainBranch.FriendlyName)
                {
                    return null;
                }

                var mergeBase = repo.ObjectDatabase.FindMergeBase(currentBranch.Tip, mainBranch.Tip);
                return mergeBase;
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Could not determine merge base: {ex.Message}");
                return null;
            }
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
                    if (item.State == FileStatus.Unaltered || item.State == FileStatus.Ignored)
                    {
                        continue;
                    }

                    var fullPath = Path.Combine(gitRootPath, item.FilePath);

                    if (filesToExclude.Contains(fullPath))
                    {
                        continue;
                    }

                    if (_supportedFileChecker.IsSupported(fullPath))
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
    }
}
