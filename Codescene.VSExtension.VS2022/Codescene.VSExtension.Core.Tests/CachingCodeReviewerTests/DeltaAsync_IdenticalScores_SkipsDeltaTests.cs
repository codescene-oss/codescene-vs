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
    public class DeltaAsync_IdenticalScores_SkipsDeltaTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IGitService> _mockGitService = null!;
        private ReviewCacheService _reviewCacheService = null!;
        private BaselineReviewCacheService _baselineCacheService = null!;
        private DeltaCacheService _deltaCacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _mockGitService = new Mock<IGitService>();
            _reviewCacheService = new ReviewCacheService();
            _baselineCacheService = new BaselineReviewCacheService();
            _deltaCacheService = new DeltaCacheService();
            _cachingReviewer = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                _reviewCacheService,
                _baselineCacheService,
                _deltaCacheService,
                _mockLogger.Object,
                _mockGitService.Object,
                null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _reviewCacheService.Clear();
            _baselineCacheService.Clear();
            _deltaCacheService.Clear();
        }

        [TestMethod]
        public async Task Test_IdenticalScores_ReturnsNull()
        {
            var filePath = "C:\\test\\file.cs";
            var currentContent = "public class Current { }";
            var oldContent = "public class Old { }";
            var identicalRawScore = "rawscore123";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 8.0f,
                RawScore = identicalRawScore,
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _baselineCacheService.Put(filePath, oldContent, identicalRawScore);

            var result = await _cachingReviewer.DeltaAsync(review, currentContent);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task Test_IdenticalScores_LogsDebugMessage()
        {
            var filePath = "C:\\test\\MyFile.cs";
            var currentContent = "public class NewVersion { }";
            var oldContent = "public class OldVersion { }";
            var identicalRawScore = "raw456";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 7.5f,
                RawScore = identicalRawScore,
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _baselineCacheService.Put(filePath, oldContent, identicalRawScore);

            await _cachingReviewer.DeltaAsync(review, currentContent);

            _mockLogger.Verify(l => l.Debug(It.Is<string>(msg => msg.Contains("Delta analysis skipped") && msg.Contains("MyFile.cs") && msg.Contains("scores identical"))), Times.Once);
        }

        [TestMethod]
        public async Task Test_IdenticalScores_DoesNotCallInnerReviewer()
        {
            var filePath = "C:\\test\\code.cs";
            var currentContent = "public class CodeV2 { }";
            var oldContent = "public class CodeV1 { }";
            var identicalRawScore = "score789";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 9.0f,
                RawScore = identicalRawScore,
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _baselineCacheService.Put(filePath, oldContent, identicalRawScore);

            await _cachingReviewer.DeltaAsync(review, currentContent);

            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task Test_IdenticalScores_CachesNullResult()
        {
            var filePath = "C:\\test\\sample.cs";
            var currentContent = "public interface ISampleNew { }";
            var oldContent = "public interface ISampleOld { }";
            var identicalRawScore = "raw999";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 6.5f,
                RawScore = identicalRawScore,
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _baselineCacheService.Put(filePath, oldContent, identicalRawScore);

            await _cachingReviewer.DeltaAsync(review, currentContent);

            var cachedDelta = _deltaCacheService.GetDeltaForFile(filePath);
            Assert.IsNull(cachedDelta);
        }

        [TestMethod]
        public async Task Test_IdenticalScoresWithPrecomputed_SkipsDelta()
        {
            var filePath = "C:\\test\\precomputed.cs";
            var currentContent = "public class Precomputed { }";
            var oldContent = "public class PrecomputedOld { }";
            var identicalRawScore = "precomputed123";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 5.0f,
                RawScore = identicalRawScore,
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);

            var result = await _cachingReviewer.DeltaAsync(review, currentContent, identicalRawScore);

            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(msg => msg.Contains("Delta analysis skipped") && msg.Contains("scores identical"))), Times.Once);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
