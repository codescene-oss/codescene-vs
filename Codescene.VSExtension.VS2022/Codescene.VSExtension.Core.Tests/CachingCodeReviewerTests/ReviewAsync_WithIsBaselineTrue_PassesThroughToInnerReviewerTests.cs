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
    public class ReviewAsync_WithIsBaselineTrue_PassesThroughToInnerReviewerTests
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
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, null, null, _mockLogger.Object, null, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "WithIsBaselineTrue_PassesThrough.cs";
            var content = "public class WithIsBaselineTruePassesThrough { }";
            var result = new FileReviewModel { FilePath = path, Score = 8.0f };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var reviewResult = await _cachingReviewer.ReviewAsync(path, content, isBaseline: true);

            Assert.IsNotNull(reviewResult);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(path, content, true, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
