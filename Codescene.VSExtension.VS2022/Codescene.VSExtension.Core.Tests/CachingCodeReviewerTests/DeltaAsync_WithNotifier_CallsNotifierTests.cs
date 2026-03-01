// Copyright (c) CodeScene. All rights reserved.

using System;
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
    public class DeltaAsync_WithNotifier_CallsNotifierTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IGitService> _mockGitService = null!;
        private Mock<ICodeHealthMonitorNotifier> _mockNotifier = null!;
        private ReviewCacheService _cacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _mockGitService = new Mock<IGitService>();
            _mockNotifier = new Mock<ICodeHealthMonitorNotifier>();
            _cacheService = new ReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                _cacheService,
                null,
                null,
                _mockLogger.Object,
                _mockGitService.Object,
                null,
                _mockNotifier.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public async Task CallsOnDeltaStartingAndOnDeltaCompleted()
        {
            var path = "TestFile.cs";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "9.5" };
            var currentCode = "current code";
            var oldCode = "old code";
            var expectedDelta = new DeltaResponseModel();

            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns(oldCode);
            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDelta);

            await _cachingReviewer.DeltaAsync(review, currentCode);

            _mockNotifier.Verify(n => n.OnDeltaStarting(path), Times.Once);
            _mockNotifier.Verify(n => n.OnDeltaCompleted(path), Times.Once);
        }

        [TestMethod]
        public async Task CallsOnDeltaCompletedEvenWhenInnerReviewerThrows()
        {
            var path = "TestFile.cs";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "9.5" };
            var currentCode = "current code";
            var oldCode = "old code";

            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns(oldCode);
            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));

            await _cachingReviewer.DeltaAsync(review, currentCode);

            _mockNotifier.Verify(n => n.OnDeltaStarting(path), Times.Once);
            _mockNotifier.Verify(n => n.OnDeltaCompleted(path), Times.Once);
        }

        [TestMethod]
        public async Task DoesNotCallNotifierWhenFilePathIsNull()
        {
            var review = new FileReviewModel { FilePath = null, Score = 8.0f, RawScore = "9.5" };
            var currentCode = "current code";

            await _cachingReviewer.DeltaAsync(review, currentCode);

            _mockNotifier.Verify(n => n.OnDeltaStarting(It.IsAny<string>()), Times.Never);
            _mockNotifier.Verify(n => n.OnDeltaCompleted(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task DoesNotCallNotifierWhenFilePathIsWhitespace()
        {
            var review = new FileReviewModel { FilePath = "   ", Score = 8.0f, RawScore = "9.5" };
            var currentCode = "current code";

            await _cachingReviewer.DeltaAsync(review, currentCode);

            _mockNotifier.Verify(n => n.OnDeltaStarting(It.IsAny<string>()), Times.Never);
            _mockNotifier.Verify(n => n.OnDeltaCompleted(It.IsAny<string>()), Times.Never);
        }
    }
}
