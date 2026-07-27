// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitService : IDisposable
    {
        string GetFileContentForCommit(string path);

        bool IsFileIgnored(string filePath);

        HashSet<string> FilterIgnoredFiles(IEnumerable<string> absolutePaths);
    }
}
