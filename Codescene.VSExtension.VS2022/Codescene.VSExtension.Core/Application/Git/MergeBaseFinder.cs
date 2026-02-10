// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    internal class MergeBaseFinder
    {
        private readonly ILogger _logger;

        public MergeBaseFinder(ILogger logger)
        {
            _logger = logger;
        }

        public Commit GetMergeBaseCommit(Repository repo)
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

        public bool IsMainBranch(string branchName)
        {
            return !string.IsNullOrEmpty(branchName) &&
                   new[] { "main", "master", "develop", "trunk", "dev" }.Contains(branchName, StringComparer.OrdinalIgnoreCase);
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
    }
}
