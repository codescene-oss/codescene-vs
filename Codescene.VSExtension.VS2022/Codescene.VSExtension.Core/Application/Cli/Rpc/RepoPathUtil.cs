// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    public static class RepoPathUtil
    {
        public static string ToRelPath(string repoRoot, string absolutePath)
        {
            if (string.IsNullOrEmpty(repoRoot) || string.IsNullOrEmpty(absolutePath))
            {
                return absolutePath?.Replace('\\', '/') ?? string.Empty;
            }

            var rootFull = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;
            var fileFull = Path.GetFullPath(absolutePath);
            if (fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                return fileFull.Substring(rootFull.Length).Replace('\\', '/');
            }

            return fileFull.Replace('\\', '/');
        }

        public static string ToAbsolutePath(string repoRoot, string relPath)
        {
            if (string.IsNullOrEmpty(repoRoot))
            {
                return relPath?.Replace('/', Path.DirectorySeparatorChar);
            }

            var relative = (relPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(repoRoot, relative));
        }

        public static string DiscoverGitRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                var discovered = LibGit2Sharp.Repository.Discover(path);
                if (string.IsNullOrEmpty(discovered))
                {
                    return null;
                }

                using (var repo = new LibGit2Sharp.Repository(discovered))
                {
                    var workingDirectory = repo.Info.WorkingDirectory;
                    if (string.IsNullOrEmpty(workingDirectory))
                    {
                        return null;
                    }

                    return Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
