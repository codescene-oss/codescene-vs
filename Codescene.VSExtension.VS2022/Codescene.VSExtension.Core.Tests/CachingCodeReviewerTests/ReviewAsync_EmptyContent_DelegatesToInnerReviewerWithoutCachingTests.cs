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
    public class ReviewAsync_EmptyContent_DelegatesToInnerReviewerWithoutCachingTests
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
            var path = "EmptyContent_DelegatesToInnerReviewer.cs";
            var content = string.Empty;

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FileReviewModel?)null);

            var result = await _cachingReviewer.ReviewAsync(path, content);

            Assert.IsNull(result);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
