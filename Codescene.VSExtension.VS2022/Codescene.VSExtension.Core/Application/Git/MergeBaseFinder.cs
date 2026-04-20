// Copyright (c) CodeScene. All rights reserved.

using System;
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
                if (repo?.Head?.Tip == null)
                {
                    return null;
                }

                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> MergeBaseFinder: Finding merge base for branch '{repo.Head.FriendlyName}'");
                #endif

                var mergeBase = MainBranchMergeBaseSelector.FindClosest(repo, _logger);
                if (mergeBase != null)
                {
                    _logger?.Debug($"GitChangeLister: Found merge base using branch reachable from HEAD ({mergeBase.Sha})");
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> MergeBaseFinder: Found merge base commit {mergeBase.Sha.Substring(0, 8)} for branch '{repo.Head.FriendlyName}'");
                    #endif
                    return mergeBase;
                }

                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> MergeBaseFinder: No merge base found for branch '{repo.Head.FriendlyName}'");
                #endif
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
            return MainBranchNames.IsMainBranch(branchName);
        }
    }
}
