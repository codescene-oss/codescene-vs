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
    public class ReviewAsync_DifferentContent_CausesNewCliCallTests
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
            var path = "DifferentContent_CausesNewCliCall.cs";
            var originalContent = "public class DifferentContentOriginal { }";
            var modifiedContent = "public class DifferentContentModified { int x; }";

            var originalResult = new FileReviewModel { FilePath = path, Score = 8.0f };
            var modifiedResult = new FileReviewModel { FilePath = path, Score = 7.5f };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, originalContent, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalResult);
            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, modifiedContent, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modifiedResult);

            var firstResult = await _cachingReviewer.ReviewAsync(path, originalContent);
            var secondResult = await _cachingReviewer.ReviewAsync(path, modifiedContent);

            Assert.AreEqual(8.0f, firstResult.Score);
            Assert.AreEqual(7.5f, secondResult.Score);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(path, originalContent, false, It.IsAny<CancellationToken>()), Times.Once);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(path, modifiedContent, false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
