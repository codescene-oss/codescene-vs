// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class ReviewWithDeltaAsync_StatusBarCompletionTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IGitService> _mockGitService = null!;
        private ReviewCacheService _reviewCacheService = null!;
        private BaselineReviewCacheService _baselineCacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _mockGitService = new Mock<IGitService>();
            _reviewCacheService = new ReviewCacheService(new ConcurrentDictionary<string, ReviewCacheItem>());
            _baselineCacheService = new BaselineReviewCacheService(new ConcurrentDictionary<string, string>());
            _cachingReviewer = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                _reviewCacheService,
                _baselineCacheService,
                null,
                _mockLogger.Object,
                _mockGitService.Object,
                null);
        }

        [TestMethod]
        public async Task CacheMiss_LogsCompletionOnStatusBar()
        {
            var path = "C:/project/test.cs";
            var content = "public class Test { }";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "current-raw" };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);
            _mockGitService.Setup(g => g.GetFileContentForCommit(path, It.IsAny<string>())).Returns(content);

            await _cachingReviewer.ReviewWithDeltaAsync(path, content);

            _mockLogger.Verify(l => l.Info("Review complete for test.cs.", true), Times.Once);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Review failed")), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task CacheHit_DoesNotLogCompletion()
        {
            var path = "C:/project/test.cs";
            var content = "public class Test { }";
            var cachedReview = new FileReviewModel { FilePath = path, Score = 9.0f, RawScore = "cached-raw" };

            _reviewCacheService.Put(new ReviewCacheEntry(content, path.ToLowerInvariant(), cachedReview));
            _baselineCacheService.Put(path, content, "baseline-raw");
            _mockGitService.Setup(g => g.GetFileContentForCommit(path, It.IsAny<string>())).Returns(content);

            await _cachingReviewer.ReviewWithDeltaAsync(path, content);

            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Review complete")), It.IsAny<bool>()), Times.Never);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task WhenCanceled_DoesNotLogCompletion()
        {
            var path = "test.cs";
            var content = "public class Test { }";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "current-raw" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);
            _mockGitService.Setup(g => g.GetFileContentForCommit(path, It.IsAny<string>())).Returns(content);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _cachingReviewer.ReviewWithDeltaAsync(path, content, cancellationToken: cts.Token));

            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Review complete")), It.IsAny<bool>()), Times.Never);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Review failed")), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task WhenInnerReviewThrows_LogsFailureOnStatusBar()
        {
            var path = "C:/project/test.cs";
            var content = "public class Test { }";

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("CLI process failed"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _cachingReviewer.ReviewWithDeltaAsync(path, content));

            _mockLogger.Verify(l => l.Info("Review failed for test.cs.", true), Times.Once);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Review complete")), It.IsAny<bool>()), Times.Never);
        }
    }
}
