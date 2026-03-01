// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class DeltaAsync_DeltaCacheHit_ReturnsCachedDeltaTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IGitService> _mockGitService = null!;
        private DeltaCacheService _deltaCacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;
        private string _tempFilePath = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _mockGitService = new Mock<IGitService>();
            _deltaCacheService = new DeltaCacheService();
            _cachingReviewer = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                null,
                null,
                _deltaCacheService,
                _mockLogger.Object,
                _mockGitService.Object,
                null);
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"DeltaAsync_DeltaCacheHit_{Guid.NewGuid()}.cs");
            File.WriteAllText(_tempFilePath, "temp content");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _deltaCacheService.Clear();
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public async Task Test()
        {
            var currentCode = "public class DeltaCacheHit { void Method() { } }";
            var baselineCode = "public class DeltaCacheHit { }";
            var cachedDelta = new DeltaResponseModel
            {
                NewScore = 8.5m,
                OldScore = 9.0m,
                ScoreChange = -0.5m,
            };
            var review = new FileReviewModel
            {
                FilePath = _tempFilePath,
                Score = 8.5f,
                RawScore = "current789",
            };

            _deltaCacheService.Put(new DeltaCacheEntry(_tempFilePath, baselineCode, currentCode, cachedDelta));
            _mockGitService.Setup(g => g.GetFileContentForCommit(_tempFilePath)).Returns(baselineCode);

            var result = await _cachingReviewer.DeltaAsync(review, currentCode);

            Assert.IsNotNull(result);
            Assert.AreEqual(cachedDelta.NewScore, result.NewScore);
            Assert.AreEqual(cachedDelta.OldScore, result.OldScore);
            Assert.AreEqual(cachedDelta.ScoreChange, result.ScoreChange);
            _mockInnerReviewer.Verify(r => r.DeltaAsync(It.IsAny<FileReviewModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
