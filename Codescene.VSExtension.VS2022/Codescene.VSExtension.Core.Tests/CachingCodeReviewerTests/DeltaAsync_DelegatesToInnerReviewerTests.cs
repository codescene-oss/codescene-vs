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
    public class DeltaAsync_DelegatesToInnerReviewerTests
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
        public async Task Test()
        {
            var path = "DeltaAsync_DelegatesToInnerReviewer.cs";
            var review = new FileReviewModel { FilePath = path, Score = 8.0f, RawScore = "9.5" };
            var currentCode = "current code";
            var oldCode = "old code";
            var precomputedScore = "baseline123";
            var expectedDelta = new DeltaResponseModel();

            _mockGitService.Setup(g => g.GetFileContentForCommit(path)).Returns(oldCode);

            _mockInnerReviewer
                .Setup(r => r.DeltaAsync(review, currentCode, precomputedScore, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDelta);

            var result = await _cachingReviewer.DeltaAsync(review, currentCode, precomputedScore);

            Assert.AreEqual(expectedDelta, result);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(review, currentCode, precomputedScore, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
