// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public static class MainBranchMergeBaseSelector
    {
        /// <summary>
        /// Returns the merge-base between HEAD and the closest known main branch (local or remote-tracking).
        /// When multiple main branches yield distinct merge bases, the one that is first reachable walking
        /// back from HEAD is chosen so a feature branched off <c>develop</c> is not baselined against <c>main</c>.
        /// </summary>
        public static Commit FindClosest(Repository repo, ILogger logger = null)
        {
            if (repo?.Head?.Tip == null)
            {
                return null;
            }

            var currentBranchName = repo.Head.FriendlyName;
            var candidates = CollectCandidates(repo, currentBranchName, logger);

            if (candidates.Count == 0)
            {
                return null;
            }

            var closest = FindClosestReachable(repo, candidates);
            return closest ?? candidates.Values.First();
        }

        private static Dictionary<string, Commit> CollectCandidates(Repository repo, string currentBranch, ILogger logger)
        {
            var mergeBases = new Dictionary<string, Commit>(StringComparer.OrdinalIgnoreCase);

            foreach (var mainBranchName in MainBranchNames.All)
            {
                TryAddCandidate(repo, currentBranch, mainBranchName, mergeBases, logger);
            }

            return mergeBases;
        }

        private static void TryAddCandidate(
            Repository repo,
            string currentBranch,
            string mainBranchName,
            IDictionary<string, Commit> mergeBases,
            ILogger logger)
        {
            var mainBranch = repo.Branches[mainBranchName]
                          ?? repo.Branches[$"origin/{mainBranchName}"];

            if (mainBranch?.Tip == null ||
                string.Equals(mainBranch.FriendlyName, currentBranch, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var mergeBase = repo.ObjectDatabase.FindMergeBase(repo.Head.Tip, mainBranch.Tip);
                if (mergeBase != null)
                {
                    mergeBases[mergeBase.Sha] = mergeBase;
                }
            }
            catch (Exception e)
            {
                logger?.Debug($"Could not find merge-base with {mainBranchName}: {e.Message}");
            }
        }

        private static Commit FindClosestReachable(Repository repo, IReadOnlyDictionary<string, Commit> mergeBases)
        {
            foreach (var commit in repo.Commits.QueryBy(new CommitFilter
                     {
                         IncludeReachableFrom = repo.Head,
                         SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
                     }))
            {
                if (mergeBases.TryGetValue(commit.Sha, out var closestMergeBase))
                {
                    return closestMergeBase;
                }
            }

            return null;
        }
    }
}
