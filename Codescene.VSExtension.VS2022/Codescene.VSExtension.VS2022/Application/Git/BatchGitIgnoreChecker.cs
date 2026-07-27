// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    public class BatchGitIgnoreChecker : IBatchGitIgnoreChecker
    {
        private readonly ILogger _logger;

        public BatchGitIgnoreChecker(ILogger logger)
        {
            _logger = logger;
        }

        public HashSet<string> FilterIgnored(IEnumerable<string> absolutePaths)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pathList = absolutePaths.ToList();

            if (pathList.Count == 0)
            {
                return result;
            }

            var pathsByRepo = GroupByRepository(pathList);

            foreach (var group in pathsByRepo)
            {
                FilterGroupByIgnoreStatus(group.Key, group.Value, result);
            }

            return result;
        }

        private static string TryDiscoverRepositoryPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var pathForDiscover = filePath;
            if (Path.IsPathRooted(filePath))
            {
                try
                {
                    pathForDiscover = Path.GetFullPath(filePath);
                }
                catch (ArgumentException)
                {
                    return null;
                }
                catch (NotSupportedException)
                {
                    return null;
                }
                catch (PathTooLongException)
                {
                    return null;
                }
                catch (IOException)
                {
                    pathForDiscover = filePath;
                }
            }

            try
            {
                return Repository.Discover(pathForDiscover);
            }
            catch (LibGit2SharpException)
            {
                return null;
            }
        }

        private static void FilterPathsInRepo(Repository repo, string repoRoot, List<string> paths, HashSet<string> result)
        {
            foreach (var absolutePath in paths)
            {
                if (PathUtilities.IsInGitDirectory(absolutePath))
                {
                    continue;
                }

                var relativePath = PathUtilities.GetRelativePath(repoRoot, absolutePath)
                    .Replace("\\", "/").Trim();

                if (string.IsNullOrEmpty(relativePath))
                {
                    relativePath = ".";
                }

                if (!repo.Ignore.IsPathIgnored(relativePath))
                {
                    result.Add(absolutePath);
                }
            }
        }

        private void FilterGroupByIgnoreStatus(string repoPath, List<string> paths, HashSet<string> result)
        {
            if (string.IsNullOrEmpty(repoPath))
            {
                result.UnionWith(paths);
                return;
            }

            try
            {
                using (var repo = new Repository(repoPath))
                {
                    var repoRoot = repo.Info.WorkingDirectory;
                    if (string.IsNullOrEmpty(repoRoot))
                    {
                        result.UnionWith(paths);
                        return;
                    }

                    FilterPathsInRepo(repo, repoRoot, paths, result);
                }
            }
            catch (LibGit2SharpException ex)
            {
                _logger.Warn($"BatchGitIgnoreChecker: LibGit2Sharp error for repo {repoPath}: {ex.Message}");
                result.UnionWith(paths);
            }
        }

        private Dictionary<string, List<string>> GroupByRepository(List<string> paths)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                var repoPath = TryDiscoverRepositoryPath(path);
                var key = repoPath ?? string.Empty;

                if (!result.ContainsKey(key))
                {
                    result[key] = new List<string>();
                }

                result[key].Add(path);
            }

            return result;
        }
    }
}
