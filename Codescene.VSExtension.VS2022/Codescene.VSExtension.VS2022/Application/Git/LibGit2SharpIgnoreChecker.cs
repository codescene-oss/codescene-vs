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
        var repoPath = Repository.Discover(filePath);
        if (string.IsNullOrEmpty(repoPath))
        {
            return false;
        }

        using (var repo = new Repository(repoPath))
        {
            var repoRoot = repo.Info.WorkingDirectory;
            var relativePath = PathUtilities.GetRelativePath(repoRoot, filePath).Replace("\\", "/");
            return repo.Ignore.IsPathIgnored(relativePath);
        }
    }

    public string GetRepositoryRoot(string filePath)
    {
        var repoPath = Repository.Discover(filePath);
        if (string.IsNullOrEmpty(repoPath))
        {
            return null;
        }

        using (var repo = new Repository(repoPath))
        {
            return repo.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar);
        }
    }
}
