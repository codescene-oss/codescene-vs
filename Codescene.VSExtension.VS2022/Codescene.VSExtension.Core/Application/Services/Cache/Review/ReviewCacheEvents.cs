using System;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review
{
    public static class ReviewCacheEvents
    {
        public static event Action<string> CacheUpdated;

        public static void OnCacheUpdated(string filePath)
        {
            CacheUpdated?.Invoke(filePath);
        }
    }
}
