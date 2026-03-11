// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    public abstract class CachingCodeReviewerTestBase
    {
        protected Mock<ICodeReviewer> _mockInnerReviewer = null!;
        protected Mock<ILogger> _mockLogger = null!;
        protected ReviewCacheService _cacheService = null!;
        protected CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void BaseSetup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _cacheService = new ReviewCacheService(new ConcurrentDictionary<string, ReviewCacheItem>(), testGenerationOverride: 0);
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, null, null, _mockLogger.Object, null, null);

            Setup();
        }

        [TestCleanup]
        public void BaseCleanup()
        {
            Cleanup();
        }

        protected virtual void Setup()
        {
        }

        protected virtual void Cleanup()
        {
        }
    }
}
