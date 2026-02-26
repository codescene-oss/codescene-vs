// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models;
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
            _cacheService = new ReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, null, null, _mockLogger.Object, _mockGitService.Object, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public async Task ReviewAsync_PassesCancellationTokenToInnerReviewer()
        {
            var path = "CancellationTokenTest.cs";
            var content = "public class CancellationTokenTest { }";
            var cts = new CancellationTokenSource();
            var specificToken = cts.Token;
            var result = new FileReviewModel { FilePath = path };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, specificToken))
                .ReturnsAsync(result);

            await _cachingReviewer.ReviewAsync(path, content, false, specificToken);

            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, content, false, specificToken),
                Times.Once,
                "Must pass the exact CancellationToken to inner reviewer");
        }

        [TestMethod]
        public async Task DeltaAsync_PassesCancellationTokenToInnerReviewer()
        {
            var review = new FileReviewModel { FilePath = "test.cs", Score = 8.0f };
            var currentCode = "current code";
            var precomputedScore = "baseline123";
            var expectedDelta = new DeltaResponseModel();
            var cts = new CancellationTokenSource();
            var specificToken = cts.Token;

            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, precomputedScore, specificToken))
                .ReturnsAsync(expectedDelta);

            await _cachingReviewer.DeltaAsync(review, currentCode, precomputedScore, specificToken);

            _mockInnerReviewer.Verify(
                r => r.DeltaAsync(review, currentCode, precomputedScore, specificToken),
                Times.Once,
                "Must pass the exact CancellationToken to inner reviewer");
        }

        [TestMethod]
        public async Task ReviewAndBaselineAsync_PassesCancellationTokenToInnerReviewer()
        {
            var path = "test.cs";
            var currentCode = "current code";
            var baselineCode = "baseline code";
            var expectedReview = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "9.5" };
            var baselineReview = new FileReviewModel { FilePath = path, RawScore = "8.0" };
            var cts = new CancellationTokenSource();
            var specificToken = cts.Token;

            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns(baselineCode);

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, currentCode, false, specificToken))
                .ReturnsAsync(expectedReview);

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, baselineCode, true, specificToken))
                .ReturnsAsync(baselineReview);

            await _cachingReviewer.ReviewAndBaselineAsync(path, currentCode, specificToken);

            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, currentCode, false, specificToken),
                Times.Once,
                "Must pass the exact CancellationToken to inner reviewer for current review");

            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, baselineCode, true, specificToken),
                Times.Once,
                "Must pass the exact CancellationToken to inner reviewer for baseline review");
        }

        [TestMethod]
        public async Task GetOrComputeBaselineRawScoreAsync_PassesCancellationTokenToInnerReviewer()
        {
            var path = "test.cs";
            var baselineContent = "baseline content";
            var expectedScore = "baseline123";
            var baselineReview = new FileReviewModel { FilePath = path, RawScore = expectedScore };
            var cts = new CancellationTokenSource();
            var specificToken = cts.Token;

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, baselineContent, true, specificToken))
                .ReturnsAsync(baselineReview);

            await _cachingReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent, specificToken);

            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(path, baselineContent, true, specificToken),
                Times.Once,
                "Must pass the exact CancellationToken to inner reviewer");
        }
    }
}
