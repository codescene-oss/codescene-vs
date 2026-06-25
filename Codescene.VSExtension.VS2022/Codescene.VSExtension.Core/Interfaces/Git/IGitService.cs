// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitService : IDisposable
    {
        event EventHandler GitIgnoreChanged;

        string GetFileContentForCommit(string path);

        bool IsFileIgnored(string filePath);
    }
}
