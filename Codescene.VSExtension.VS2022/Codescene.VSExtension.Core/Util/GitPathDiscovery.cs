// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Util
{
    public static class GitPathDiscovery
    {
        public static IReadOnlyList<string> FindNestedRepositories(string directory)
        {
            var found = new List<string>();
            foreach (var child in ChildDirectories(directory))
            {
                var root = TryGetWorkingDirectory(child);
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                if (SameDirectory(root, child))
                {
                    found.Add(root);
                }
            }

            return found;
        }

        public static string TryGetWorkingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                var repoPath = Repository.Discover(path);
                if (string.IsNullOrEmpty(repoPath))
                {
                    return null;
                }

                using (var repo = new Repository(repoPath))
                {
                    var workingDirectory = repo.Info.WorkingDirectory;
                    if (string.IsNullOrEmpty(workingDirectory))
                    {
                        return null;
                    }

                    return PathNormalization.NormalizeWorkingDirectory(workingDirectory);
                }
            }
            catch
            {
                return null;
            }
        }

        public static (string workspacePath, string gitRootPath) Discover(string solutionPath)
        {
            var workspacePath = Directory.Exists(solutionPath)
                ? solutionPath
                : Path.GetDirectoryName(solutionPath);
            var gitRootPath = workspacePath;

            var repoPath = Repository.Discover(solutionPath);
            if (!string.IsNullOrEmpty(repoPath))
            {
                using (var repo = new Repository(repoPath))
                {
                    var workingDirectory = repo.Info.WorkingDirectory;
                    if (string.IsNullOrEmpty(workingDirectory))
                    {
                        gitRootPath = workingDirectory;
                    }
                    else
                    {
                        var root = Path.GetPathRoot(workingDirectory);
                        var normalizedWorking = workingDirectory.TrimEnd(Path.DirectorySeparatorChar);
                        var normalizedRoot = root?.TrimEnd(Path.DirectorySeparatorChar) ?? string.Empty;
                        gitRootPath = string.Equals(normalizedWorking, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                            ? workingDirectory
                            : workingDirectory.TrimEnd(Path.DirectorySeparatorChar);
                    }
                }
            }

            return (workspacePath, gitRootPath);
        }

        public static FileSystemWatcher CreateWatcher(string path)
        {
            return new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            };
        }

        private static IEnumerable<string> ChildDirectories(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            try
            {
                var children = new List<string>();
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (!IsExcludedFromScan(Path.GetFileName(child)))
                    {
                        children.Add(child);
                    }
                }

                return children;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static bool IsExcludedFromScan(string name)
        {
            if (string.IsNullOrEmpty(name) || name[0] == '.')
            {
                return true;
            }

            return name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameDirectory(string left, string right)
        {
            return string.Equals(
                PathNormalization.NormalizeWorkingDirectory(left),
                PathNormalization.NormalizeWorkingDirectory(right),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
