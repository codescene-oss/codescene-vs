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
            return Select(repo, logger).Commit;
        }

        public static MergeBaseSelection Select(Repository repo, ILogger logger = null)
        {
            if (repo?.Head?.Tip == null)
            {
                return default;
            }

            var currentBranchName = repo.Head.FriendlyName;
            var candidates = CollectCandidates(repo, currentBranchName, logger);

            if (candidates.MergeBases.Count == 0)
            {
                return default;
            }

            var closestSha = FindClosestReachableSha(repo, candidates.MergeBases);
            if (closestSha == null)
            {
                closestSha = candidates.MergeBases.Keys.First();
            }

            candidates.MergeBases.TryGetValue(closestSha, out var commit);
            candidates.BaselineBranches.TryGetValue(closestSha, out var baselineBranchName);
            return new MergeBaseSelection(commit, baselineBranchName);
        }

        private static CandidateCollection CollectCandidates(Repository repo, string currentBranch, ILogger logger)
        {
            var candidates = new CandidateCollection();

            var defaultBranch = MainBranchNames.GetDefaultBranch(repo);
            if (!string.IsNullOrEmpty(defaultBranch))
            {
                TryAddCandidate(repo, currentBranch, defaultBranch, candidates, logger);
                return candidates;
            }

            foreach (var mainBranchName in MainBranchNames.All)
            {
                TryAddCandidate(repo, currentBranch, mainBranchName, candidates, logger);
            }

            return candidates;
        }

        private static void TryAddCandidate(
            Repository repo,
            string currentBranch,
            string mainBranchName,
            CandidateCollection candidates,
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
                    candidates.MergeBases[mergeBase.Sha] = mergeBase;
                    candidates.BaselineBranches[mergeBase.Sha] = mainBranchName;
                }
            }
            catch (Exception e)
            {
                logger?.Debug($"Could not find merge-base with {mainBranchName}: {e.Message}");
            }
        }

        private static string FindClosestReachableSha(Repository repo, IReadOnlyDictionary<string, Commit> mergeBases)
        {
            foreach (var commit in repo.Commits.QueryBy(new CommitFilter
                     {
                         IncludeReachableFrom = repo.Head,
                         SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
                     }))
            {
                if (mergeBases.ContainsKey(commit.Sha))
                {
                    return commit.Sha;
                }
            }

            return null;
        }

        private sealed class CandidateCollection
        {
            public Dictionary<string, Commit> MergeBases { get; } = new Dictionary<string, Commit>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> BaselineBranches { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
