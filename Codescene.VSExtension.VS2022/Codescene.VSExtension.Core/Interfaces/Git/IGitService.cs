// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitService : IDisposable
    {
        bool IsFileIgnored(string filePath);
    }
}
