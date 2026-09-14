// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    /// <summary>
    /// Collects the paths changed by the commits on the first-parent path between a base commit and HEAD.
    /// Merge commits contribute no paths of their own, so merging an advanced baseline into the current
    /// branch does not bring the whole baseline into the change set.
    /// </summary>
    public static class FirstParentChangeCollector
    {
        public static IReadOnlyCollection<string> CollectChangedPaths(Repository repo, Commit baseCommit)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);

            if (repo?.Head?.Tip == null || baseCommit == null)
            {
                return paths;
            }

            foreach (var commit in QueryFirstParentCommits(repo, baseCommit))
            {
                if (commit.Parents.Count() > 1)
                {
                    continue;
                }

                AddChangedPaths(repo, commit, paths);
            }

            return paths;
        }

        private static IEnumerable<Commit> QueryFirstParentCommits(Repository repo, Commit baseCommit)
        {
            return repo.Commits.QueryBy(new CommitFilter
            {
                IncludeReachableFrom = repo.Head.Tip,
                ExcludeReachableFrom = baseCommit,
                FirstParentOnly = true,
                SortBy = CommitSortStrategies.Topological,
            });
        }

        private static void AddChangedPaths(Repository repo, Commit commit, HashSet<string> paths)
        {
            var parentTree = commit.Parents.FirstOrDefault()?.Tree;

            foreach (var change in repo.Diff.Compare<TreeChanges>(parentTree, commit.Tree))
            {
                paths.Add(change.Path);
            }
        }
    }
}
