using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review
{
    public class ReviewCacheService : CacheService<ReviewCacheQuery, ReviewCacheEntry, ReviewCacheItem, FileReviewModel>
    {
        public override FileReviewModel Get(ReviewCacheQuery query)
        {
            string filePath = query.FilePath;
            string fileContents = query.FileContents;
            string contentHash = Hash(fileContents);

            if (Cache.TryGetValue(filePath, out var cachedItem))
            {
                bool cacheHit = cachedItem.FileContentsHash == contentHash;
                return cacheHit ? cachedItem.Response : null;
            }

            return null;
        }

        public override void Put(ReviewCacheEntry entry)
        {
            string filePath = entry.FilePath;
            string fileContents = entry.FileContents;
            string contentHash = Hash(fileContents);

            var cacheItem = new ReviewCacheItem(contentHash, entry.Response);
            Cache[filePath] = cacheItem;
        }
    }
}