// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitService : IDisposable
    {
        string GetBaselineCommit(string repoRootPath);

        string GetFileContentForCommit(string path);

        string GetFileContentForCommit(string path, string baselineCommit);

        bool IsFileIgnored(string filePath);

        HashSet<string> FilterIgnoredFiles(IEnumerable<string> absolutePaths);
    }
}
