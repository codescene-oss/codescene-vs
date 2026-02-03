// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
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
        public void Review_NullPath_ReturnsNull()
        {
            // Arrange
            string? path = null;
            var content = "some code";

            // Act
            var result = _codeReviewer.Review(path, content);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Skipping review"))), Times.Once);
        }

        [TestMethod]
        public void Review_EmptyPath_ReturnsNull()
        {
            // Arrange
            var path = string.Empty;
            var content = "some code";

            // Act
            var result = _codeReviewer.Review(path, content);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Review_WhitespacePath_ReturnsNull()
        {
            // Arrange
            var path = "   ";
            var content = "some code";

            // Act
            var result = _codeReviewer.Review(path, content);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Review_NullContent_ReturnsNull()
        {
            // Arrange
            var path = "test.cs";
            string? content = null;

            // Act
            var result = _codeReviewer.Review(path, content);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Skipping review"))), Times.Once);
        }

        [TestMethod]
        public void Review_EmptyContent_ReturnsNull()
        {
            // Arrange
            var path = "test.cs";
            var content = string.Empty;

            // Act
            var result = _codeReviewer.Review(path, content);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Review_WhitespaceContent_ReturnsNull()
        {
            // Arrange
            var path = "test.cs";
            var content = "   ";

            // Act
            var result = _codeReviewer.Review(path, content);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Review_ValidInput_CallsExecutorAndMapper()
        {
            // Arrange
            var path = "C:/project/test.cs";
            var content = "public class Test { }";
            var cliReview = new CliReviewModel { Score = 8.5f, RawScore = "raw123" };
            var expectedResult = new FileReviewModel { FilePath = path, Score = 8.5f };

            _mockExecutor.Setup(x => x.ReviewContent("test.cs", content)).Returns(cliReview);
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(expectedResult);

            // Act
            var result = _codeReviewer.Review(path, content);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(path, result.FilePath);
            Assert.AreEqual(8.5f, result.Score);
            _mockExecutor.Verify(x => x.ReviewContent("test.cs", content), Times.Once);
            _mockMapper.Verify(x => x.Map(path, cliReview), Times.Once);
        }

        [TestMethod]
        public void Review_ExtractsFileNameFromPath()
        {
            // Arrange
            var path = "C:/some/deep/path/to/MyFile.cs";
            var content = "code";
            var cliReview = new CliReviewModel();

            _mockExecutor.Setup(x => x.ReviewContent("MyFile.cs", content)).Returns(cliReview);
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>())).Returns(new FileReviewModel());

            // Act
            _codeReviewer.Review(path, content);

            // Assert - verify executor was called with just the filename, not the full path
            _mockExecutor.Verify(x => x.ReviewContent("MyFile.cs", content), Times.Once);
        }

        [TestMethod]
        public void Delta_NullFilePath_ReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = null, RawScore = "raw" };

            // Act
            var result = _codeReviewer.Delta(review, "current code");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("missing file path"))), Times.Once);
        }

        [TestMethod]
        public void Delta_EmptyFilePath_ReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = string.Empty, RawScore = "raw" };

            // Act
            var result = _codeReviewer.Delta(review, "current code");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("missing file path"))), Times.Once);
        }

        [TestMethod]
        public void Delta_WhitespaceFilePath_ReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = "   ", RawScore = "raw" };

            // Act
            var result = _codeReviewer.Delta(review, "current code");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Delta_WhenExceptionThrown_LogsErrorAndReturnsNull()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = "raw" };
            var expectedException = new Exception("Git error");

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Throws(expectedException);

            // Act
            var result = _codeReviewer.Delta(review, "current code");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("test.cs")), expectedException), Times.Once);
        }

        [TestMethod]
        public void Delta_NullRawScore_UsesEmptyString()
        {
            // Arrange
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = null };

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns("old code");
            _mockExecutor.Setup(x => x.ReviewContent(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new CliReviewModel { RawScore = "old-raw" });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = "old-raw" });
            _mockExecutor.Setup(x => x.ReviewDelta(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new Models.Cli.Delta.DeltaResponseModel());

            // Act - should not throw
            var result = _codeReviewer.Delta(review, "current code");

            // Assert - verify ReviewDelta was called with empty string for current raw score
            _mockExecutor.Verify(x => x.ReviewDelta(It.IsAny<string>(), string.Empty), Times.Once);
        }

        [TestMethod]
        public void Delta_IdenticalContent_ReturnsNullAndSkipsDeltaAnalysis()
        {
            // Arrange
            var currentCode = "public class Test { }";
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = "raw-score" };

            // Git returns the same content as current (no changes since baseline)
            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns(currentCode);

            // Act
            var result = _codeReviewer.Delta(review, currentCode);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("content unchanged since baseline"))), Times.Once);

            // ReviewDelta should NOT be called since content is identical
            _mockExecutor.Verify(x => x.ReviewDelta(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void Delta_IdenticalScores_ReturnsNullAndSkipsDeltaAnalysis()
        {
            // Arrange
            var oldCode = "public class Test { int x = 1; }";
            var currentCode = "public class Test { int y = 1; }";
            var identicalRawScore = "identical-raw-score";
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = identicalRawScore };

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns(oldCode);
            _mockExecutor.Setup(x => x.ReviewContent(It.IsAny<string>(), oldCode))
                .Returns(new CliReviewModel { RawScore = identicalRawScore });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = identicalRawScore });

            // Act
            var result = _codeReviewer.Delta(review, currentCode);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("scores are identical"))), Times.Once);

            // ReviewDelta should NOT be called since scores are identical
            _mockExecutor.Verify(x => x.ReviewDelta(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void Delta_DifferentContent_CallsReviewDelta()
        {
            // Arrange
            var oldCode = "public class Test { }";
            var currentCode = "public class Test { int x; }";
            var review = new FileReviewModel { FilePath = "test.cs", RawScore = "new-raw" };

            _mockGitService.Setup(x => x.GetFileContentForCommit(It.IsAny<string>()))
                .Returns(oldCode);
            _mockExecutor.Setup(x => x.ReviewContent(It.IsAny<string>(), oldCode))
                .Returns(new CliReviewModel { RawScore = "old-raw" });
            _mockMapper.Setup(x => x.Map(It.IsAny<string>(), It.IsAny<CliReviewModel>()))
                .Returns(new FileReviewModel { RawScore = "old-raw" });
            _mockExecutor.Setup(x => x.ReviewDelta("old-raw", "new-raw"))
                .Returns(new Models.Cli.Delta.DeltaResponseModel { ScoreChange = -0.5m });

            // Act
            var result = _codeReviewer.Delta(review, currentCode);

            // Assert
            Assert.IsNotNull(result);
            _mockExecutor.Verify(x => x.ReviewDelta("old-raw", "new-raw"), Times.Once);
        }
    }
}
