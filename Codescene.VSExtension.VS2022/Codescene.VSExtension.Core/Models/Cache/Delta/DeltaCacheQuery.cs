// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cache.Delta
{
    public class DeltaCacheQuery
    {
        public DeltaCacheQuery(string filePath, string baselineContent, string currentContent)
        {
            FilePath = filePath;
            BaselineContent = baselineContent;
            CurrentContent = currentContent;
        }

        public string FilePath { get; }

        public string BaselineContent { get; }

        public string CurrentContent { get; }
    }
}
