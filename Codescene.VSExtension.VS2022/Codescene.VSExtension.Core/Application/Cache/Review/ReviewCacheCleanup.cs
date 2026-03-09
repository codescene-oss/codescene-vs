// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public static class ReviewCacheCleanup
    {
        public static void CleanupCachesOutsideRoot(string gitRootPath)
        {
            new DeltaCacheService().RemoveEntriesOutsideRoot(gitRootPath);
            new ReviewCacheService().RemoveEntriesOutsideRoot(gitRootPath);
            new BaselineReviewCacheService().RemoveEntriesOutsideRoot(gitRootPath);
        }

        public static void InvalidateFile(string filePath)
        {
            new DeltaCacheService().Invalidate(filePath);
            new BaselineReviewCacheService().Invalidate(filePath);
        }
    }
}
