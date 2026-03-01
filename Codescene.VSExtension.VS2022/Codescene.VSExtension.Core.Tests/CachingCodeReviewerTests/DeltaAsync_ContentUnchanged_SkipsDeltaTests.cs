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
    public class DeltaAsync_ContentUnchanged_SkipsDeltaTests
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
        public async Task Test_ContentUnchanged_ReturnsNull()
        {
            var filePath = "C:\\test\\file.cs";
            var content = "public class Test { }";
            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 8.0f,
                RawScore = "rawscore123",
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(content);

            var result = await _cachingReviewer.DeltaAsync(review, content);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task Test_ContentUnchanged_LogsDebugMessage()
        {
            var filePath = "C:\\test\\MyFile.cs";
            var content = "public class MyClass { }";
            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 7.5f,
                RawScore = "raw456",
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(content);

            await _cachingReviewer.DeltaAsync(review, content);

            _mockLogger.Verify(l => l.Debug(It.Is<string>(msg => msg.Contains("Delta analysis skipped") && msg.Contains("MyFile.cs") && msg.Contains("content unchanged"))), Times.Once);
        }

        [TestMethod]
        public async Task Test_ContentUnchanged_DoesNotCallInnerReviewer()
        {
            var filePath = "C:\\test\\code.cs";
            var content = "using System; public class Code { }";
            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 9.0f,
                RawScore = "score789",
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(content);

            await _cachingReviewer.DeltaAsync(review, content);

            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task Test_ContentUnchanged_CachesNullResult()
        {
            var filePath = "C:\\test\\sample.cs";
            var content = "public interface ISample { }";
            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 6.5f,
                RawScore = "raw999",
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(content);

            await _cachingReviewer.DeltaAsync(review, content);

            var cachedDelta = _deltaCacheService.GetDeltaForFile(filePath);
            Assert.IsNull(cachedDelta);
        }

        [TestMethod]
        public async Task Test_EmptyContentUnchanged_SkipsDelta()
        {
            var filePath = "C:\\test\\empty.cs";
            var content = string.Empty;
            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 5.0f,
                RawScore = "empty123",
            };

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(string.Empty);

            var result = await _cachingReviewer.DeltaAsync(review, content);

            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(msg => msg.Contains("Delta analysis skipped") && msg.Contains("content unchanged"))), Times.Once);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
