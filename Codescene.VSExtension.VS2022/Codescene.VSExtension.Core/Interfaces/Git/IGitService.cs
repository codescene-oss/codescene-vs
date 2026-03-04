// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitService : IDisposable
    {
        string GetFileContentForCommit(string path);

        bool IsFileIgnored(string filePath);
    }
}
