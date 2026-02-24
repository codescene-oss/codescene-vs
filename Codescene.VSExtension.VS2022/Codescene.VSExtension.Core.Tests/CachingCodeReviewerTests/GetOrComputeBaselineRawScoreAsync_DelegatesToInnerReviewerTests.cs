// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class GetOrComputeBaselineRawScoreAsync_DelegatesToInnerReviewerTests
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
            var path = "GetOrComputeBaselineRawScoreAsync_DelegatesToInnerReviewer.cs";
            var baselineContent = "baseline content";
            var expectedScore = "baseline123";

            _mockInnerReviewer
                .Setup(r => r.GetOrComputeBaselineRawScoreAsync(path, baselineContent, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedScore);

            var result = await _cachingReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent);

            Assert.AreEqual(expectedScore, result);
            _mockInnerReviewer.Verify(r => r.GetOrComputeBaselineRawScoreAsync(path, baselineContent, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
