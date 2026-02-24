// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class DeltaAsync_DelegatesToInnerReviewerTests
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
            var review = new FileReviewModel { FilePath = "DeltaAsync_DelegatesToInnerReviewer.cs", Score = 8.0f };
            var currentCode = "current code";
            var precomputedScore = "baseline123";
            var expectedDelta = new DeltaResponseModel();

            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, precomputedScore, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDelta);

            var result = await _cachingReviewer.DeltaAsync(review, currentCode, precomputedScore);

            Assert.AreEqual(expectedDelta, result);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(review, currentCode, precomputedScore, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
