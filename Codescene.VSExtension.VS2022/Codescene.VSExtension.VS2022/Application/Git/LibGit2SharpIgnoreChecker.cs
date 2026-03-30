// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.VS2022.Application.Git;

public class LibGit2SharpIgnoreChecker : IGitIgnoreChecker
{
    public bool IsPathIgnored(string filePath)
    {
        var repoPath = TryDiscoverRepositoryPath(filePath);
        if (string.IsNullOrEmpty(repoPath))
        {
            return false;
        }

        try
        {
            using (var repo = new Repository(repoPath))
            {
                var repoRoot = repo.Info.WorkingDirectory;
                var relativePath = PathUtilities.GetRelativePath(repoRoot, filePath).Replace("\\", "/");
                return repo.Ignore.IsPathIgnored(relativePath);
            }
        }
        catch (LibGit2SharpException)
        {
            return false;
        }
    }

    public string GetRepositoryRoot(string filePath)
    {
        var repoPath = TryDiscoverRepositoryPath(filePath);
        if (string.IsNullOrEmpty(repoPath))
        {
            return null;
        }

        try
        {
            using (var repo = new Repository(repoPath))
            {
                return repo.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar);
            }
        }
        catch (LibGit2SharpException)
        {
            return null;
        }
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
}
