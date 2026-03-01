// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class DeltaAsync_WithTelemetryManager_SendsTelemetryTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IGitService> _mockGitService = null!;
        private Mock<ITelemetryManager> _mockTelemetryManager = null!;
        private ReviewCacheService _reviewCacheService = null!;
        private BaselineReviewCacheService _baselineCacheService = null!;
        private DeltaCacheService _deltaCacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;
        private string _tempDir = null!;
        private List<string> _tempFiles = new List<string>();

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "CodeSceneTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);

            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _mockGitService = new Mock<IGitService>();
            _mockTelemetryManager = new Mock<ITelemetryManager>();
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
                _mockTelemetryManager.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _reviewCacheService.Clear();
            _baselineCacheService.Clear();
            _deltaCacheService.Clear();

            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public async Task Test_DeltaComputed_WithTelemetryManager()
        {
            var filePath = CreateTempFile("file.cs");
            var currentContent = "public class Current { }";
            var oldContent = "public class Old { }";
            var currentRawScore = "current123";
            var oldRawScore = "old456";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 8.0f,
                RawScore = currentRawScore,
            };

            var expectedDelta = CreateDeltaResponse(0.5m);

            _mockTelemetryManager
                .Setup(t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _baselineCacheService.Put(filePath, oldContent, oldRawScore);
            _mockInnerReviewer.Setup(r => r.DeltaAsync(
                It.IsAny<FileReviewModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(expectedDelta);

            var result = await _cachingReviewer.DeltaAsync(review, currentContent);

            await Task.Delay(100);
            _mockTelemetryManager.Verify(
                t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
                Times.AtLeastOnce());

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedDelta, result);
        }

        [TestMethod]
        public async Task Test_DeltaComputedWithDifferentScores_WithTelemetryManager()
        {
            var filePath = CreateTempFile("MyFile.cs");
            var currentContent = "public class NewVersion { }";
            var oldContent = "public class OldVersion { }";
            var currentRawScore = "raw789";
            var oldRawScore = "raw123";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 7.5f,
                RawScore = currentRawScore,
            };

            var expectedDelta = CreateDeltaResponse(-1.0m);

            _mockTelemetryManager
                .Setup(t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _baselineCacheService.Put(filePath, oldContent, oldRawScore);
            _mockInnerReviewer.Setup(r => r.DeltaAsync(
                It.IsAny<FileReviewModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(expectedDelta);

            var result = await _cachingReviewer.DeltaAsync(review, currentContent);

            await Task.Delay(100);
            _mockTelemetryManager.Verify(
                t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
                Times.AtLeastOnce());

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedDelta, result);
        }

        [TestMethod]
        public async Task Test_NullTelemetryManager_DoesNotThrow()
        {
            var filePath = CreateTempFile("code.cs");
            var currentContent = "public class CodeV2 { }";
            var oldContent = "public class CodeV1 { }";
            var currentRawScore = "score999";
            var oldRawScore = "score111";

            var cachingReviewerWithoutTelemetry = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                _reviewCacheService,
                _baselineCacheService,
                _deltaCacheService,
                _mockLogger.Object,
                _mockGitService.Object,
                null);

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 9.0f,
                RawScore = currentRawScore,
            };

            var expectedDelta = CreateDeltaResponse(0.0m);

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _baselineCacheService.Put(filePath, oldContent, oldRawScore);
            _mockInnerReviewer.Setup(r => r.DeltaAsync(
                It.IsAny<FileReviewModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(expectedDelta);

            var result = await cachingReviewerWithoutTelemetry.DeltaAsync(review, currentContent);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task Test_DeltaCacheMiss_WithTelemetryManager()
        {
            var filePath = CreateTempFile("sample.cs");
            var currentContent = "public interface ISampleNew { }";
            var oldContent = "public interface ISampleOld { }";
            var currentRawScore = "raw555";
            var oldRawScore = "raw666";

            var review = new FileReviewModel
            {
                FilePath = filePath,
                Score = 6.5f,
                RawScore = currentRawScore,
            };

            var expectedDelta = CreateDeltaResponse(-0.5m);

            _mockTelemetryManager
                .Setup(t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath)).Returns(oldContent);
            _mockInnerReviewer.Setup(r => r.ReviewAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new FileReviewModel { RawScore = oldRawScore });
            _mockInnerReviewer.Setup(r => r.DeltaAsync(
                It.IsAny<FileReviewModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(expectedDelta);

            var result = await _cachingReviewer.DeltaAsync(review, currentContent);

            await Task.Delay(100);
            _mockTelemetryManager.Verify(
                t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
                Times.AtLeastOnce());

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedDelta, result);
        }

        [TestMethod]
        public async Task Test_MultipleDeltaComputations_WithTelemetryManager()
        {
            var filePath1 = CreateTempFile("file1.cs");
            var filePath2 = CreateTempFile("file2.cs");
            var currentContent1 = "public class File1 { }";
            var currentContent2 = "public class File2 { }";
            var oldContent1 = "public class File1Old { }";
            var oldContent2 = "public class File2Old { }";

            var review1 = new FileReviewModel
            {
                FilePath = filePath1,
                Score = 8.0f,
                RawScore = "score1",
            };

            var review2 = new FileReviewModel
            {
                FilePath = filePath2,
                Score = 7.0f,
                RawScore = "score2",
            };

            _mockTelemetryManager
                .Setup(t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath1)).Returns(oldContent1);
            _mockGitService.Setup(g => g.GetFileContentForCommit(filePath2)).Returns(oldContent2);
            _baselineCacheService.Put(filePath1, oldContent1, "oldScore1");
            _baselineCacheService.Put(filePath2, oldContent2, "oldScore2");
            _mockInnerReviewer.Setup(r => r.DeltaAsync(
                It.IsAny<FileReviewModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(CreateDeltaResponse(0.5m));

            var result1 = await _cachingReviewer.DeltaAsync(review1, currentContent1);
            var result2 = await _cachingReviewer.DeltaAsync(review2, currentContent2);

            await Task.Delay(100);
            _mockTelemetryManager.Verify(
                t => t.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
                Times.AtLeastOnce());

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
        }

        private static DeltaResponseModel CreateDeltaResponse(decimal scoreChange)
        {
            return new DeltaResponseModel
            {
                ScoreChange = scoreChange,
                OldScore = 8.0m,
                NewScore = 8.0m + scoreChange,
                FileLevelFindings = new ChangeDetailModel[0],
                FunctionLevelFindings = new FunctionFindingModel[0],
            };
        }

        private string CreateTempFile(string fileName)
        {
            var filePath = Path.Combine(_tempDir, fileName);
            File.WriteAllText(filePath, string.Empty);
            _tempFiles.Add(filePath);
            return filePath;
        }
    }
}
