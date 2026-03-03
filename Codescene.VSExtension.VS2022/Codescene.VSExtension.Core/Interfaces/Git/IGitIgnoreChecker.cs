// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitIgnoreChecker
    {
        bool IsPathIgnored(string filePath);

        string GetRepositoryRoot(string filePath);
    }
}
