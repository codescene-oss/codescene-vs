// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public class ReviewCacheService : CacheService<ReviewCacheQuery, ReviewCacheEntry, ReviewCacheItem, FileReviewModel>
    {
        public override FileReviewModel Get(ReviewCacheQuery query)
        {
            string cacheKey = GetCacheKey(query.FilePath, query.IsBaseline);
            string fileContents = query.FileContents;
            string contentHash = Hash(fileContents);

            if (Cache.TryGetValue(cacheKey, out var cachedItem))
            {
                bool cacheHit = cachedItem.FileContentsHash == contentHash && cachedItem.IsBaseline == query.IsBaseline;
                return cacheHit ? cachedItem.Response : null;
            }

            return null;
        }

        public override void Put(ReviewCacheEntry entry)
        {
            string cacheKey = GetCacheKey(entry.FilePath, entry.IsBaseline);
            string fileContents = entry.FileContents;
            string contentHash = Hash(fileContents);

            var cacheItem = new ReviewCacheItem(contentHash, entry.Response, entry.IsBaseline);
            Cache[cacheKey] = cacheItem;
        }

        public override void Invalidate(string key)
        {
            base.Invalidate(GetCacheKey(key, false));
            base.Invalidate(GetCacheKey(key, true));
        }

        public override void UpdateKey(string oldKey, string newKey)
        {
            if (Cache.TryGetValue(GetCacheKey(oldKey, false), out var entryFalse))
            {
                Cache[GetCacheKey(newKey, false)] = entryFalse;
                base.Invalidate(GetCacheKey(oldKey, false));
            }

            if (Cache.TryGetValue(GetCacheKey(oldKey, true), out var entryTrue))
            {
                Cache[GetCacheKey(newKey, true)] = entryTrue;
                base.Invalidate(GetCacheKey(oldKey, true));
            }
        }

        private string GetCacheKey(string filePath, bool isBaseline)
        {
            return filePath + "|baseline=" + isBaseline;
        }
    }
}
