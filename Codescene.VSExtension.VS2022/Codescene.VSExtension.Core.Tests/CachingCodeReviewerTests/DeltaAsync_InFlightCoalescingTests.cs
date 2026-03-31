// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class DeltaAsync_InFlightCoalescingTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IGitService> _mockGitService = null!;
        private ReviewCacheService _cacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _mockGitService = new Mock<IGitService>();
            _cacheService = new ReviewCacheService(new ConcurrentDictionary<string, ReviewCacheItem>());
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, null, null, _mockLogger.Object, _mockGitService.Object, null);
        }

        [TestMethod]
        public async Task DeltaAsync_ConcurrentIdenticalRequests_CallsInnerReviewerOnce()
        {
            var path = "coalesce.cs";
            var review = new FileReviewModel { FilePath = path, RawScore = "new-raw", Score = 7.5f };
            var currentCode = "new code";
            var oldCode = "old code";
            var precomputedBaselineRawScore = "old-raw";
            var expectedDelta = new DeltaResponseModel();
            var entered = new TaskCompletionSource<bool>();
            var gate = new TaskCompletionSource<bool>();

            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns(oldCode);
            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, precomputedBaselineRawScore, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    entered.TrySetResult(true);
                    await gate.Task;
                    return expectedDelta;
                });

            var first = _cachingReviewer.DeltaAsync(review, currentCode, precomputedBaselineRawScore);
            var second = _cachingReviewer.DeltaAsync(review, currentCode, precomputedBaselineRawScore);

            await entered.Task;
            _mockInnerReviewer.Verify(
                r => r.DeltaAsync(review, currentCode, precomputedBaselineRawScore, It.IsAny<long?>(), It.IsAny<CancellationToken>()),
                Times.Once);

            gate.TrySetResult(true);
            var firstResult = await first;
            var secondResult = await second;

            Assert.AreEqual(expectedDelta, firstResult);
            Assert.AreEqual(expectedDelta, secondResult);
            _mockInnerReviewer.Verify(
                r => r.DeltaAsync(review, currentCode, precomputedBaselineRawScore, It.IsAny<long?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task DeltaAsync_ConcurrentRequestsWithDifferentBaselineScores_CallInnerReviewerTwice()
        {
            var path = "coalesce.cs";
            var review = new FileReviewModel { FilePath = path, RawScore = "new-raw", Score = 7.5f };
            var currentCode = "new code";
            var oldCode = "old code";
            var firstBaselineRawScore = "old-raw-1";
            var secondBaselineRawScore = "old-raw-2";
            var firstEntered = new TaskCompletionSource<bool>();
            var secondEntered = new TaskCompletionSource<bool>();
            var gate = new TaskCompletionSource<bool>();

            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns(oldCode);
            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, firstBaselineRawScore, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    firstEntered.TrySetResult(true);
                    await gate.Task;
                    return new DeltaResponseModel();
                });
            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, secondBaselineRawScore, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    secondEntered.TrySetResult(true);
                    await gate.Task;
                    return new DeltaResponseModel();
                });

            var first = _cachingReviewer.DeltaAsync(review, currentCode, firstBaselineRawScore);
            var second = _cachingReviewer.DeltaAsync(review, currentCode, secondBaselineRawScore);

            await Task.WhenAll(firstEntered.Task, secondEntered.Task);

            _mockInnerReviewer.Verify(
                r => r.DeltaAsync(review, currentCode, firstBaselineRawScore, It.IsAny<long?>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _mockInnerReviewer.Verify(
                r => r.DeltaAsync(review, currentCode, secondBaselineRawScore, It.IsAny<long?>(), It.IsAny<CancellationToken>()),
                Times.Once);

            gate.TrySetResult(true);
            await Task.WhenAll(first, second);
        }
    }
}
