// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class ReviewAndBaselineAsync_CacheHit_ReturnsCachedReviewTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IGitService> _mockGitService = null!;
        private ReviewCacheService _reviewCacheService = null!;
        private BaselineReviewCacheService _baselineCacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _mockGitService = new Mock<IGitService>();
            _reviewCacheService = new ReviewCacheService();
            _baselineCacheService = new BaselineReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                _reviewCacheService,
                _baselineCacheService,
                null,
                _mockLogger.Object,
                _mockGitService.Object,
                null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _reviewCacheService.Clear();
            _baselineCacheService.Clear();
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "ReviewAndBaselineAsync_CacheHit.cs";
            var currentCode = "public class CacheHit { }";
            var baselineCode = "public class CacheHit { int x; }";
            var cachedReview = new FileReviewModel
            {
                FilePath = path,
                Score = 9.0f,
                RawScore = "current123",
            };
            var cachedBaselineRawScore = "baseline456";

            _reviewCacheService.Put(new ReviewCacheEntry(currentCode, path.ToLowerInvariant(), cachedReview));
            _baselineCacheService.Put(path, baselineCode, cachedBaselineRawScore);
            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns(baselineCode);

            var result = await _cachingReviewer.ReviewAndBaselineAsync(path, currentCode);

            Assert.IsNotNull(result.review);
            Assert.AreEqual(cachedReview.Score, result.review.Score);
            Assert.AreEqual(cachedReview.RawScore, result.review.RawScore);
            Assert.AreEqual(cachedBaselineRawScore, result.baselineRawScore);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
