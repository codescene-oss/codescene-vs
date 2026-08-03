// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
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
    public class ReviewWithDeltaAsync_BaselineCommit_UsesProvidedCommitTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer;
        private Mock<ILogger> _mockLogger;
        private Mock<IGitService> _mockGitService;
        private ReviewCacheService _cacheService;
        private CachingCodeReviewer _cachingReviewer;

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
        public async Task DeltaAsync_WithBaselineCommit_UsesProvidedCommitForFileContent()
        {
            var path = "test.cs";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "9.5" };
            var currentCode = "current code";
            var oldCode = "old code";
            var baselineCommit = "abc123";
            var expectedDelta = new DeltaResponseModel();

            _mockGitService.Setup(g => g.GetFileContentForCommit(path, baselineCommit)).Returns(oldCode);

            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDelta);

            await _cachingReviewer.DeltaAsync(review, currentCode, null, null, default, baselineCommit);

            _mockGitService.Verify(g => g.GetFileContentForCommit(path, baselineCommit), Times.Once);
            _mockGitService.Verify(g => g.GetFileContentForCommit(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task DeltaAsync_WithoutBaselineCommit_CallsSingleArgOverload()
        {
            var path = "test.cs";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "9.5" };
            var currentCode = "current code";
            var oldCode = "old code";
            var expectedDelta = new DeltaResponseModel();

            _mockGitService.Setup(g => g.GetFileContentForCommit(path, null)).Returns(oldCode);

            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDelta);

            await _cachingReviewer.DeltaAsync(review, currentCode, null, null, default, null);

            _mockGitService.Verify(g => g.GetFileContentForCommit(path, null), Times.Once);
        }
    }
}
