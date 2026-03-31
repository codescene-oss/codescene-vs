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
    public class CancellationTokenTests
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
        public async Task DeltaAsync_CanceledCallerDoesNotCancelSharedDeltaComputation()
        {
            var path = "test.cs";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "current-raw" };
            var currentCode = "current code";
            var precomputedScore = "baseline123";
            var expectedDelta = new DeltaResponseModel();
            var entered = new TaskCompletionSource<bool>();
            var gate = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource();
            var callerToken = cts.Token;
            var capturedToken = CancellationToken.None;

            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns("old code");
            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, precomputedScore, null, It.IsAny<CancellationToken>()))
                .Returns<FileReviewModel, string, string, long?, CancellationToken>(async (_, _, _, _, token) =>
                {
                    capturedToken = token;
                    entered.TrySetResult(true);
                    await gate.Task;
                    return expectedDelta;
                });

            var canceledCallerTask = _cachingReviewer.DeltaAsync(review, currentCode, precomputedScore, null, callerToken);
            await entered.Task;
            var secondCallerTask = _cachingReviewer.DeltaAsync(review, currentCode, precomputedScore);
            cts.Cancel();
            gate.TrySetResult(true);

            await Assert.ThrowsAsync<OperationCanceledException>(() => canceledCallerTask);

            var secondResult = await secondCallerTask;

            Assert.AreEqual(expectedDelta, secondResult);
            Assert.AreEqual(CancellationToken.None, capturedToken);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(review, currentCode, precomputedScore, null, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
