// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class DeltaAsync_NullFilePath_ReturnsNullTests
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
            _reviewCacheService = new ReviewCacheService();
            _baselineCacheService = new BaselineReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                _reviewCacheService,
                _baselineCacheService,
                null,
                _mockLogger.Object,
                _mockGitService.Object,
                null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _reviewCacheService.Clear();
            _baselineCacheService.Clear();
        }

        [TestMethod]
        public async Task Test_NullFilePath()
        {
            var review = new FileReviewModel
            {
                FilePath = null,
                Score = 8.0f,
                RawScore = "rawscore123",
            };
            var currentCode = "public class Test { }";

            var result = await _cachingReviewer.DeltaAsync(review, currentCode);

            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn("Could not review file, missing file path."), Times.Once);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task Test_EmptyFilePath()
        {
            var review = new FileReviewModel
            {
                FilePath = string.Empty,
                Score = 8.0f,
                RawScore = "rawscore123",
            };
            var currentCode = "public class Test { }";

            var result = await _cachingReviewer.DeltaAsync(review, currentCode);

            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn("Could not review file, missing file path."), Times.Once);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task Test_WhitespaceFilePath()
        {
            var review = new FileReviewModel
            {
                FilePath = "   ",
                Score = 8.0f,
                RawScore = "rawscore123",
            };
            var currentCode = "public class Test { }";

            var result = await _cachingReviewer.DeltaAsync(review, currentCode);

            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn("Could not review file, missing file path."), Times.Once);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
