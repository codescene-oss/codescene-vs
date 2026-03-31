// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cache.Review
{
    public class ReviewCacheEntry
    {
        public ReviewCacheEntry(string fileContents, string filePath, FileReviewModel response, bool isBaseline = false)
        {
            FileContents = fileContents;
            FilePath = filePath;
            Response = response;
            IsBaseline = isBaseline;
        }

        public string FileContents { get; }

        public string FilePath { get; }

        public FileReviewModel Response { get; }

        public bool IsBaseline { get; }
    }
}
