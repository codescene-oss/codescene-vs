// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cache.Review
{
    public class ReviewCacheQuery
    {
        public ReviewCacheQuery(string fileContents, string filePath)
        {
            FileContents = fileContents;
            FilePath = filePath;
        }

        public string FileContents { get; }

        public string FilePath { get; }
    }
}
