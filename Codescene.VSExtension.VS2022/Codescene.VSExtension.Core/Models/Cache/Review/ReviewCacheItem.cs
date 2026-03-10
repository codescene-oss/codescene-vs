// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cache.Review
{
    public class ReviewCacheItem
    {
        public ReviewCacheItem(string fileContentsHash, FileReviewModel response, bool isBaseline = false, long rulesGeneration = 0)
        {
            FileContentsHash = fileContentsHash;
            Response = response;
            IsBaseline = isBaseline;
            RulesGeneration = rulesGeneration;
        }

        public string FileContentsHash { get; }

        public FileReviewModel Response { get; }

        public bool IsBaseline { get; }

        public long RulesGeneration { get; }
    }
}
