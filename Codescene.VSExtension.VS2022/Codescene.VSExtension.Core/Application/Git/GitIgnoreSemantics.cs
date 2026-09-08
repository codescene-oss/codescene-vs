// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public static class GitIgnoreSemantics
    {
        public static bool IsPathIgnoredConsideringIndex(Repository repo, string relativePath)
        {
            if (repo.Index[relativePath] != null || repo.Index.Conflicts[relativePath] != null)
            {
                return false;
            }

            return repo.Ignore.IsPathIgnored(relativePath);
        }
    }
}
