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
    public class ReviewAndBaselineAsync_DelegatesToInnerReviewerTests
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
            var path = "ReviewAndBaselineAsync_DelegatesToInnerReviewer.cs";
            var currentCode = "current code";
            var expectedReview = new FileReviewModel { FilePath = path, Score = 8.0f };
            var expectedBaselineScore = "baseline123";

            _mockInnerReviewer
                .Setup(r => r.ReviewAndBaselineAsync(path, currentCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync((expectedReview, expectedBaselineScore));

            var result = await _cachingReviewer.ReviewAndBaselineAsync(path, currentCode);

            Assert.AreEqual(expectedReview, result.review);
            Assert.AreEqual(expectedBaselineScore, result.baselineRawScore);
            _mockInnerReviewer.Verify(r => r.ReviewAndBaselineAsync(path, currentCode, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
