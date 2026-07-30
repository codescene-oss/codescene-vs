// Copyright (c) CodeScene. All rights reserved.

using System;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class DefaultBranchGate
    {
        private readonly string _gitRootPath;
        private readonly object _cacheLock = new object();
        private string _cachedDefaultBranch;
        private bool _hasFetched;

        public DefaultBranchGate(string gitRootPath)
        {
            _gitRootPath = gitRootPath;
        }

        public bool ShouldSkip(Repository repo)
        {
            if (repo == null)
            {
                return false;
            }

            var currentBranch = repo.Head?.FriendlyName;
            if (string.IsNullOrEmpty(currentBranch))
            {
                return false;
            }

            var defaultBranch = GetDefaultBranchCached(repo);
            if (string.IsNullOrEmpty(defaultBranch))
            {
                return false;
            }

            return string.Equals(currentBranch, defaultBranch, StringComparison.OrdinalIgnoreCase);
        }

        public bool ShouldSkipForCurrentBranch()
        {
            if (string.IsNullOrEmpty(_gitRootPath))
            {
                return false;
            }

            try
            {
                var repoPath = Repository.Discover(_gitRootPath);
                if (string.IsNullOrEmpty(repoPath))
                {
                    return false;
                }

                using (var repo = new Repository(repoPath))
                {
                    return ShouldSkip(repo);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedDefaultBranch = null;
                _hasFetched = false;
            }
        }

        private string GetDefaultBranchCached(Repository repo)
        {
            lock (_cacheLock)
            {
                if (!_hasFetched)
                {
                    _cachedDefaultBranch = MainBranchNames.GetDefaultBranch(repo);
                    _hasFetched = true;
                }

                return _cachedDefaultBranch;
            }
        }
    }
}
