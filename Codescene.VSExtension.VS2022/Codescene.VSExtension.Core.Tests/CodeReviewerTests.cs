// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CodeReviewerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<IModelMapper> _mockMapper;
        private Mock<ICliExecutor> _mockExecutor;
        private Mock<ITelemetryManager> _mockTelemetryManager;
        private Mock<IGitService> _mockGitService;
        private CodeReviewer _codeReviewer;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockMapper = new Mock<IModelMapper>();
            _mockExecutor = new Mock<ICliExecutor>();
            _mockTelemetryManager = new Mock<ITelemetryManager>();
            _mockGitService = new Mock<IGitService>();

            _codeReviewer = new CodeReviewer(
                _mockLogger.Object,
                _mockMapper.Object,
                _mockExecutor.Object,
                _mockTelemetryManager.Object,
                _mockGitService.Object);
        }

        [TestMethod]
        public async Task ReviewAsync_NullPath_ReturnsNull()
        {
            // Arrange
            string? path = null;
            var content = "some code";

            // Act
            var result = await _codeReviewer.ReviewAsync(path, content);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Skipping review"))), Times.Once);
        }

        [TestMethod]
        public async Task ReviewAsync_EmptyPath_ReturnsNull()
        {
            // Arrange
            var path = string.Empty;
            var content = "some code";

            // Act
            var result = await _codeReviewer.ReviewAsync(path, content);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReviewAsync_WhitespacePath_ReturnsNull()
        {
            // Arrange
            var path = "   ";
            var content = "some code";

            // Act
            var result = await _codeReviewer.ReviewAsync(path, content);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReviewAsync_NullContent_ReturnsNull()
        {
            // Arrange
            var path = "test.cs";
            string? content = null;

            // Act
            var result = await _codeReviewer.ReviewAsync(path, content);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Skipping review"))), Times.Once);
        }

        [TestMethod]
        public async Task ReviewAsync_EmptyContent_ReturnsNull()
        {
            var path = "test.cs";
            var content = string.Empty;

            var result = await _codeReviewer.ReviewAsync(path, content);

            Assert.IsNull(result);
            _mockExecutor.Verify(x => x.ReviewContentAsync(It.IsAny<string>(), It.Is<string>(s => string.IsNullOrWhiteSpace(s)), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task ReviewAsync_WhitespaceContent_ReturnsNull()
        {
            var path = "test.cs";
            var content = "   ";

            var result = await _codeReviewer.ReviewAsync(path, content);

            Assert.IsNull(result);
            _mockExecutor.Verify(x => x.ReviewContentAsync(It.IsAny<string>(), It.Is<string>(s => string.IsNullOrWhiteSpace(s)), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task ReviewAsync_ValidInput_CallsExecutorAndMapper()
        {
            // Arrange
            var path = "C:/project/test.cs";
            var content = "public class Test { }";
            var cliReview = new CliReviewModel { Score = 8.5f, RawScore = "raw123" };
            var expectedResult = new FileReviewModel { FilePath = path, Score = 8.5f };

            _mockExecutor.Setup(x => x.ReviewContentAsync("test.cs", content, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(cliReview);
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(expectedResult);

            // Act
            var result = await _codeReviewer.ReviewAsync(path, content);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(path, result.FilePath);
            Assert.AreEqual(8.5f, result.Score);
            _mockExecutor.Verify(x => x.ReviewContentAsync("test.cs", content, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockMapper.Verify(x => x.Map(path, cliReview), Times.Once);
        }

        [TestMethod]
        public async Task ReviewAsync_ExtractsFileNameFromPath()
        {
            // Arrange
            var path = "C:/some/deep/path/to/MyFile.cs";
            var content = "code";
            var cliReview = new CliReviewModel();

            _mockExecutor.Setup(x => x.ReviewContentAsync("MyFile.cs", content, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(cliReview);
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>())).Returns(new FileReviewModel());

            // Act
            await _codeReviewer.ReviewAsync(path, content);

            // Assert
            _mockExecutor.Verify(x => x.ReviewContentAsync("MyFile.cs", content, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task DeltaAsync_NullFilePath_ReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = null, RawScore = "raw" };

            // Act
            var result = await _codeReviewer.DeltaAsync(review, "current code");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("missing file path"))), Times.Once);
        }

        [TestMethod]
        public async Task DeltaAsync_EmptyFilePath_ReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = string.Empty, RawScore = "raw" };

            // Act
            var result = await _codeReviewer.DeltaAsync(review, "current code");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("missing file path"))), Times.Once);
        }

        [TestMethod]
        public async Task DeltaAsync_WhitespaceFilePath_ReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = "   ", RawScore = "raw" };

            // Act
            var result = await _codeReviewer.DeltaAsync(review, "current code");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task DeltaAsync_WhenExceptionThrown_LogsErrorAndReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = "raw" };
            var expectedException = new Exception("Git error");

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Throws(expectedException);

            // Act
            var result = await _codeReviewer.DeltaAsync(review, "current code");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("test.cs")), expectedException), Times.Once);
        }

        [TestMethod]
        public async Task DeltaAsync_NullRawScore_UsesEmptyString()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = null };

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns("old code");
            _mockExecutor.Setup(x => x.ReviewContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliReviewModel { RawScore = "old-raw" });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = "old-raw" });
            _mockExecutor.Setup(x => x.ReviewDeltaAsync(It.IsAny<ReviewDeltaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeltaResponseModel());

            // Act - should not throw
            await _codeReviewer.DeltaAsync(review, "current code");

            // Assert
            _mockExecutor.Verify(x => x.ReviewDeltaAsync(It.Is<ReviewDeltaRequest>(r => r.NewScore == string.Empty), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task DeltaAsync_IdenticalContent_ReturnsNullAndSkipsDeltaAnalysis()
        {
            // Arrange
            var currentCode = "public class Test { }";
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = "raw-score" };

            // Git returns the same content as current (no changes since baseline)
            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns(currentCode);

            // Act
            var result = await _codeReviewer.DeltaAsync(review, currentCode);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("content unchanged since baseline"))), Times.Once);

            _mockExecutor.Verify(x => x.ReviewDeltaAsync(It.IsAny<ReviewDeltaRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task DeltaAsync_IdenticalScores_ReturnsNullAndSkipsDeltaAnalysis()
        {
            // Arrange
            var oldCode = "public class Test { int x = 1; }";
            var currentCode = "public class Test { int y = 1; }";
            var identicalRawScore = "identical-raw-score";
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = identicalRawScore };

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns(oldCode);
            _mockExecutor.Setup(x => x.ReviewContentAsync(It.IsAny<string>(), oldCode, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliReviewModel { RawScore = identicalRawScore });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = identicalRawScore });

            // Act
            var result = await _codeReviewer.DeltaAsync(review, currentCode);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("scores are identical"))), Times.Once);

            _mockExecutor.Verify(x => x.ReviewDeltaAsync(It.IsAny<ReviewDeltaRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task DeltaAsync_DifferentContent_CallsReviewDelta()
        {
            // Arrange
            var oldCode = "public class Test { }";
            var currentCode = "public class Test { int x; }";
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = "new-raw" };

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns(oldCode);
            _mockExecutor.Setup(x => x.ReviewContentAsync(It.IsAny<string>(), oldCode, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliReviewModel { RawScore = "old-raw" });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = "old-raw" });
            _mockExecutor.Setup(x => x.ReviewDeltaAsync(It.IsAny<ReviewDeltaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeltaResponseModel { ScoreChange = -0.5m });

            // Act
            var result = await _codeReviewer.DeltaAsync(review, currentCode);

            // Assert
            Assert.IsNotNull(result);
            _mockExecutor.Verify(x => x.ReviewDeltaAsync(It.Is<ReviewDeltaRequest>(r => r.OldScore == "old-raw" && r.NewScore == "new-raw" && r.FilePath == "test.cs" && r.FileContent == currentCode), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ReviewAndBaselineAsync_ReturnsReviewAndBaselineRawScore()
        {
            var path = "test.cs";
            var currentCode = "public class Test { }";
            var oldCode = "public class OldTest { }";
            var review = new FileReviewModel { FilePath = path, RawScore = "current-raw", Score = 8.5f };
            var cliReview = new CliReviewModel { RawScore = "current-raw" };
            var baselineCliReview = new CliReviewModel { RawScore = "baseline-raw" };

            _mockGitService.Setup(x => x.GetFileContentForCommit(path)).Returns(oldCode);
            _mockExecutor.Setup(x => x.ReviewContentAsync("test.cs", currentCode, false, It.IsAny<CancellationToken>())).ReturnsAsync(cliReview);
            _mockExecutor.Setup(x => x.ReviewContentAsync("test.cs", oldCode, true, It.IsAny<CancellationToken>())).ReturnsAsync(baselineCliReview);
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(review);
            _mockMapper.Setup(x => x.Map(path, baselineCliReview)).Returns(new FileReviewModel { FilePath = path, RawScore = "baseline-raw", Score = 7.0f });

            var (actualReview, actualBaseline) = await _codeReviewer.ReviewAndBaselineAsync(path, currentCode);

            Assert.IsNotNull(actualReview);
            Assert.AreEqual("current-raw", actualReview.RawScore);
            Assert.AreEqual("baseline-raw", actualBaseline);
        }

        [TestMethod]
        public async Task ReviewAndBaselineAsync_WhenGitReturnsNull_UsesEmptyStringForOldCode()
        {
            var path = "test.cs";
            var currentCode = "public class Test { }";
            var cliReview = new CliReviewModel { RawScore = "current-raw" };
            var review = new FileReviewModel { FilePath = path, RawScore = "current-raw" };

            _mockGitService.Setup(x => x.GetFileContentForCommit(path)).Returns((string)null);
            _mockExecutor.Setup(x => x.ReviewContentAsync("test.cs", currentCode, false, It.IsAny<CancellationToken>())).ReturnsAsync(cliReview);
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(review);

            var (actualReview, actualBaseline) = await _codeReviewer.ReviewAndBaselineAsync(path, currentCode);

            Assert.IsNotNull(actualReview);
            Assert.AreEqual(string.Empty, actualBaseline);
            _mockExecutor.Verify(x => x.ReviewContentAsync(It.IsAny<string>(), It.Is<string>(s => string.IsNullOrWhiteSpace(s)), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task GetOrComputeBaselineRawScoreAsync_WhenBaselineContentEmpty_ReturnsEmptyWithoutCallingExecutor()
        {
            var path = "test.cs";
            var baselineContent = string.Empty;

            var result = await _codeReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent);

            Assert.AreEqual(string.Empty, result);
            _mockExecutor.Verify(x => x.ReviewContentAsync(It.IsAny<string>(), It.Is<string>(s => string.IsNullOrWhiteSpace(s)), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task GetOrComputeBaselineRawScoreAsync_WhenCached_ReturnsCachedScore()
        {
            var path = "test.cs";
            var baselineContent = "cached content";

            _mockExecutor.Setup(x => x.ReviewContentAsync(It.IsAny<string>(), baselineContent, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliReviewModel { RawScore = "cached-raw" });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = "cached-raw" });

            var result = await _codeReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent);
            var secondResult = await _codeReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent);

            Assert.AreEqual("cached-raw", result);
            Assert.AreEqual("cached-raw", secondResult);
            _mockExecutor.Verify(x => x.ReviewContentAsync(It.IsAny<string>(), baselineContent, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrComputeBaselineRawScoreAsync_WhenReviewReturnsNullRawScore_DoesNotCache()
        {
            var path = "uncached.cs";
            var baselineContent = "content without raw score";

            _mockExecutor.Setup(x => x.ReviewContentAsync(It.IsAny<string>(), baselineContent, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliReviewModel { RawScore = null });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = null });

            var result = await _codeReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent);

            var secondResult = await _codeReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent);

            Assert.AreEqual(string.Empty, result);
            Assert.AreEqual(string.Empty, secondResult);
            _mockExecutor.Verify(x => x.ReviewContentAsync(It.IsAny<string>(), baselineContent, true, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
