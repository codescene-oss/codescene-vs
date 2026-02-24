// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
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
    public class ReviewAsync_MultipleConcurrentRequests_OnlyOneCliCallTests
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
            var path = "MultipleConcurrentRequests_OnlyOneCliCall.cs";
            var content = "public class MultipleConcurrentRequestsOnlyOneCliCall { }";
            var result = new FileReviewModel { FilePath = path, Score = 8.5f };

            var tcs = new TaskCompletionSource<FileReviewModel>();
            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var task1 = _cachingReviewer.ReviewAsync(path, content);
            var task2 = _cachingReviewer.ReviewAsync(path, content);
            var task3 = _cachingReviewer.ReviewAsync(path, content);

            tcs.SetResult(result);

            var results = await Task.WhenAll(task1, task2, task3);

            Assert.HasCount(3, results);
            Assert.IsTrue(
                results.All(r => r != null && r.Score == 8.5f),
                "All results should be identical");

            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()),
                Times.Once,
                "Concurrent requests with same content should coalesce into ONE CLI call");
        }
    }
}
