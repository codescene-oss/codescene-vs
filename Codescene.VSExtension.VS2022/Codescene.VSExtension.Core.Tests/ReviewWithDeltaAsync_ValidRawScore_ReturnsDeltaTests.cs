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
    public class ReviewWithDeltaAsync_ValidRawScore_ReturnsDeltaTests
    {
        private Mock<ILogger> _mockLogger = null!;
        private Mock<IModelMapper> _mockMapper = null!;
        private Mock<ICliExecutor> _mockExecutor = null!;
        private Mock<ITelemetryManager> _mockTelemetryManager = null!;
        private Mock<IGitService> _mockGitService = null!;
        private CodeReviewer _codeReviewer = null!;

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
        public async Task Test()
        {
            var path = "test.cs";
            var content = "public class Test { }";
            var oldCode = "public class OldTest { }";
            var review = new FileReviewModel { FilePath = path, RawScore = "current-raw", Score = 8.5f };
            var cliReview = new CliReviewModel { RawScore = "current-raw" };
            var baselineCliReview = new CliReviewModel { RawScore = "baseline-raw" };
            var expectedDelta = new DeltaResponseModel { ScoreChange = 0.5m };

            _mockGitService.Setup(x => x.GetFileContentForCommit(path)).Returns(oldCode);
            _mockExecutor.Setup(x => x.ReviewContentAsync("test.cs", content, false, It.IsAny<CancellationToken>())).ReturnsAsync(cliReview);
            _mockExecutor.Setup(x => x.ReviewContentAsync("test.cs", oldCode, true, It.IsAny<CancellationToken>())).ReturnsAsync(baselineCliReview);
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(review);
            _mockMapper.Setup(x => x.Map(path, baselineCliReview)).Returns(new FileReviewModel { FilePath = path, RawScore = "baseline-raw" });
            _mockExecutor.Setup(x => x.ReviewDeltaAsync(It.IsAny<ReviewDeltaRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(expectedDelta);

            var (actualReview, actualDelta) = await _codeReviewer.ReviewWithDeltaAsync(path, content);

            Assert.IsNotNull(actualReview);
            Assert.AreEqual("current-raw", actualReview.RawScore);
            Assert.IsNotNull(actualDelta);
            Assert.AreEqual(0.5m, actualDelta.ScoreChange);
            _mockExecutor.Verify(x => x.ReviewDeltaAsync(It.Is<ReviewDeltaRequest>(r => r.NewScore == "current-raw" && r.OldScore == "baseline-raw"), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
