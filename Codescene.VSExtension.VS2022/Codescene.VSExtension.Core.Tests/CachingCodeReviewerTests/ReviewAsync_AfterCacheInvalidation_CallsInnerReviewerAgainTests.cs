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
    public class ReviewAsync_AfterCacheInvalidation_CallsInnerReviewerAgainTests
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
            var path = "AfterCacheInvalidation_CallsInnerReviewerAgain.cs";
            var content = "public class AfterCacheInvalidationCallsInnerReviewerAgain { }";
            var originalResult = new FileReviewModel { FilePath = path, Score = 8.0f };
            var newResult = new FileReviewModel { FilePath = path, Score = 9.0f };

            _mockInnerReviewer
                .SetupSequence(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalResult)
                .ReturnsAsync(newResult);

            var firstResult = await _cachingReviewer.ReviewAsync(path, content);
            Assert.AreEqual(8.0f, firstResult.Score);

            _cacheService.Invalidate(path);

            var secondResult = await _cachingReviewer.ReviewAsync(path, content);

            Assert.AreEqual(9.0f, secondResult.Score, "Should get fresh result after invalidation");
            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()),
                Times.Exactly(2),
                "Inner reviewer should be called twice - once before and once after invalidation");
        }
    }
}
