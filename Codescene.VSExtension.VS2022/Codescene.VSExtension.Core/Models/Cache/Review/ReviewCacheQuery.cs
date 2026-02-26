// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cache.Review
{
    public class ReviewCacheQuery
    {
        public ReviewCacheQuery(string fileContents, string filePath, bool isBaseline = false)
        {
            FileContents = fileContents;
            FilePath = filePath;
            IsBaseline = isBaseline;
        }

        public string FileContents { get; }

        public string FilePath { get; }

        public bool IsBaseline { get; }
    }
}
