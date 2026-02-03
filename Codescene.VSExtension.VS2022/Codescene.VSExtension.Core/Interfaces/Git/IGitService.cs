// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitService
    {
        string GetFileContentForCommit(string path);

        bool IsFileIgnored(string filePath);
    }
}
