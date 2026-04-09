// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public static class ReviewCacheCleanup
    {
        public static bool CleanupCaches(string gitRootPath)
        {
            var deltaCache = new DeltaCacheService();
            var reviewCache = new ReviewCacheService();
            var baselineCache = new BaselineReviewCacheService();
            var didCleanup = false;
            didCleanup |= deltaCache.RemoveEntriesOutsideRoot(gitRootPath);
            didCleanup |= deltaCache.CleanupOldGenerations();

            didCleanup |= reviewCache.RemoveEntriesOutsideRoot(gitRootPath);

            didCleanup |= baselineCache.RemoveEntriesOutsideRoot(gitRootPath);
            return didCleanup;
        }

        public static void InvalidateFile(string filePath)
        {
            new DeltaCacheService().Invalidate(filePath);
            new BaselineReviewCacheService().Invalidate(filePath);
            new ReviewCacheService().Invalidate(filePath);
        }
    }
}
