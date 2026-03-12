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
        public async Task DeltaAsync_PassesCancellationTokenToInnerReviewer()
        {
            var review = new FileReviewModel { FilePath = "test.cs", Score = 8.0f };
            var currentCode = "current code";
            var precomputedScore = "baseline123";
            var expectedDelta = new DeltaResponseModel();
            var cts = new CancellationTokenSource();
            var specificToken = cts.Token;

            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, precomputedScore, null, specificToken))
                .ReturnsAsync(expectedDelta);

            await _cachingReviewer.DeltaAsync(review, currentCode, precomputedScore, null, specificToken);

            _mockInnerReviewer.Verify(
                r => r.DeltaAsync(review, currentCode, precomputedScore, null, specificToken),
                Times.Once,
                "Must pass the exact CancellationToken to inner reviewer");
        }
    }
}
