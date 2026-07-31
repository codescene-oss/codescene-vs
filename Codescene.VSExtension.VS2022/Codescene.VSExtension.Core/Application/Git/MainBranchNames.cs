// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public static class MainBranchNames
    {
        public static readonly IReadOnlyList<string> All = new[]
        {
            "main", "master", "develop", "development", "trunk", "dev",
        };

        private const string OriginHeadRef = "refs/remotes/origin/HEAD";
        private const string OriginRemotePrefix = "refs/remotes/origin/";

        private static readonly Dictionary<string, string> _defaultBranchCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();

        public static string GetDefaultBranch(Repository repo)
        {
            try
            {
                var gitRoot = repo?.Info?.WorkingDirectory;
                if (string.IsNullOrEmpty(gitRoot))
                {
                    return GetDefaultBranchUncached(repo);
                }

                var normalizedRoot = gitRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                lock (_cacheLock)
                {
                    if (_defaultBranchCache.TryGetValue(normalizedRoot, out var cached))
                    {
                        return cached;
                    }
                }

                var result = GetDefaultBranchUncached(repo);

                if (result != null)
                {
                    lock (_cacheLock)
                    {
                        _defaultBranchCache[normalizedRoot] = result;
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static void ClearDefaultBranchCache(string gitRootPath = null)
        {
            lock (_cacheLock)
            {
                if (string.IsNullOrEmpty(gitRootPath))
                {
                    _defaultBranchCache.Clear();
                    return;
                }

                var normalizedRoot = gitRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                _defaultBranchCache.Remove(normalizedRoot);
            }
        }

        public static bool IsMainBranch(Repository repo, string branchName)
        {
            if (string.IsNullOrEmpty(branchName))
            {
                return false;
            }

            var defaultBranch = GetDefaultBranch(repo);
            if (!string.IsNullOrEmpty(defaultBranch))
            {
                return string.Equals(branchName, defaultBranch, StringComparison.OrdinalIgnoreCase);
            }

            return All.Contains(branchName, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsMainBranch(string branchName)
        {
            return !string.IsNullOrEmpty(branchName)
                && All.Contains(branchName, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetDefaultBranchUncached(Repository repo)
        {
            var gitRoot = repo?.Info?.WorkingDirectory;
            if (!string.IsNullOrEmpty(gitRoot))
            {
                var configured = CodesceneRepoConfig.GetBaselineBranch(gitRoot);
                if (!string.IsNullOrEmpty(configured))
                {
                    return configured;
                }
            }

            var originHead = repo?.Refs[OriginHeadRef];
            if (originHead == null)
            {
                return null;
            }

            var target = originHead.TargetIdentifier;
            if (string.IsNullOrEmpty(target))
            {
                return null;
            }

            if (target.StartsWith(OriginRemotePrefix, StringComparison.Ordinal))
            {
                return target.Substring(OriginRemotePrefix.Length);
            }

            return null;
        }
    }
}
