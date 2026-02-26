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
    public class ReviewAsync_SameContent_UsesCacheOnSecondCallTests
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
            var path = "SameContent_UsesCacheOnSecondCall.cs";
            var content = "public class SameContentUsesCacheOnSecondCall { }";
            var result = new FileReviewModel { FilePath = path, Score = 8.5f };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var firstResult = await _cachingReviewer.ReviewAsync(path, content);
            var secondResult = await _cachingReviewer.ReviewAsync(path, content);

            Assert.AreEqual(firstResult.Score, secondResult.Score);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
