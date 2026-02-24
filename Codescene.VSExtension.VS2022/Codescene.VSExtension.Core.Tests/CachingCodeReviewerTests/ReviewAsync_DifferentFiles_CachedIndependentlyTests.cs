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
    public class ReviewAsync_DifferentFiles_CachedIndependentlyTests
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
            var content = "public class DifferentFilesCachedIndependently { }";
            var file1 = "DifferentFiles_CachedIndependently_1.cs";
            var file2 = "DifferentFiles_CachedIndependently_2.cs";
            var result1 = new FileReviewModel { FilePath = file1, Score = 8.0f };
            var result2 = new FileReviewModel { FilePath = file2, Score = 9.0f };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(file1, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result1);
            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(file2, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result2);

            var firstResult = await _cachingReviewer.ReviewAsync(file1, content);
            var secondResult = await _cachingReviewer.ReviewAsync(file2, content);

            Assert.AreEqual(8.0f, firstResult.Score);
            Assert.AreEqual(9.0f, secondResult.Score);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(file1, content, false, It.IsAny<CancellationToken>()), Times.Once);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(file2, content, false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
