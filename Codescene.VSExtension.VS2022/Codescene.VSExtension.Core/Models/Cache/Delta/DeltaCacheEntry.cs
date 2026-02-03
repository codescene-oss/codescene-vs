// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Models.Cache.Delta
{
    public class DeltaCacheEntry
    {
        public string FilePath { get; }

        public string BaselineContent { get; }

        public string CurrentFileContent { get; }

        public DeltaResponseModel Delta { get; }

        public DeltaCacheEntry(string filePath, string baselineContent, string currentFileContent, DeltaResponseModel delta)
        {
            FilePath = filePath;
            BaselineContent = baselineContent;
            CurrentFileContent = currentFileContent;
            Delta = delta;
        }
    }
}
