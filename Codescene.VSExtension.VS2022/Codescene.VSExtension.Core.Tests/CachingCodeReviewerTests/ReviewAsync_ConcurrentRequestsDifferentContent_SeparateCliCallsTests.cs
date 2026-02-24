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
    public class ReviewAsync_ConcurrentRequestsDifferentContent_SeparateCliCallsTests
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
            var path = "test.cs";
            var content1 = "public class Test { }";
            var content2 = "public class Test { int x; }";
            var result1 = new FileReviewModel { FilePath = path, Score = 8.0f };
            var result2 = new FileReviewModel { FilePath = path, Score = 7.5f };

            var tcs1 = new TaskCompletionSource<FileReviewModel>();
            var tcs2 = new TaskCompletionSource<FileReviewModel>();

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content1, false, It.IsAny<CancellationToken>()))
                .Returns(tcs1.Task);
            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content2, false, It.IsAny<CancellationToken>()))
                .Returns(tcs2.Task);

            var task1 = _cachingReviewer.ReviewAsync(path, content1);
            var task2 = _cachingReviewer.ReviewAsync(path, content2);

            tcs1.SetResult(result1);
            tcs2.SetResult(result2);

            var results = await Task.WhenAll(task1, task2);

            Assert.AreEqual(8.0f, results[0].Score);
            Assert.AreEqual(7.5f, results[1].Score);
            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, content1, false, It.IsAny<CancellationToken>()),
                Times.Once);
            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, content2, false, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
