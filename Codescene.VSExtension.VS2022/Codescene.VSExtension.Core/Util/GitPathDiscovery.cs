// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Util
{
    public static class GitPathDiscovery
    {
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
    }
}
