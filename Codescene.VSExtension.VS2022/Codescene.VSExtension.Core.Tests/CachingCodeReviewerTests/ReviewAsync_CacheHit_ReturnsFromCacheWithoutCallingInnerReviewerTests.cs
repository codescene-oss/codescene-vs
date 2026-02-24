// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class ReviewAsync_CacheHit_ReturnsFromCacheWithoutCallingInnerReviewerTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private ReviewCacheService _cacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _cacheService = new ReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, _mockLogger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "CacheHit_ReturnsFromCache.cs";
            var content = "public class CacheHitReturnsFromCache { }";
            var cachedResult = new FileReviewModel
            {
                FilePath = path,
                Score = 9.0f,
                RawScore = "cached123",
            };

            _cacheService.Put(new Models.Cache.Review.ReviewCacheEntry(content, path, cachedResult));

            var result = await _cachingReviewer.ReviewAsync(path, content);

            Assert.IsNotNull(result);
            Assert.AreEqual(cachedResult.Score, result.Score);
            Assert.AreEqual(cachedResult.RawScore, result.RawScore);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
