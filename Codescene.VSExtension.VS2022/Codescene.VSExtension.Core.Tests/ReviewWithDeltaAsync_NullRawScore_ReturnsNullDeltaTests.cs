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
    public class ReviewWithDeltaAsync_NullRawScore_ReturnsNullDeltaTests
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
            var reviewWithNullRawScore = new FileReviewModel { FilePath = path, RawScore = null, Score = 8.0f };
            var cliReview = new CliReviewModel { RawScore = null };

            _mockGitService.Setup(x => x.GetFileContentForCommit(path)).Returns("old code");
            _mockExecutor.Setup(x => x.ReviewContentAsync("test.cs", content, false, It.IsAny<CancellationToken>())).ReturnsAsync(cliReview);
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(reviewWithNullRawScore);

            var (actualReview, actualDelta) = await _codeReviewer.ReviewWithDeltaAsync(path, content);

            Assert.IsNotNull(actualReview);
            Assert.AreEqual(path, actualReview.FilePath);
            Assert.IsNull(actualReview.RawScore);
            Assert.IsNull(actualDelta);
            _mockExecutor.Verify(x => x.ReviewDeltaAsync(It.IsAny<ReviewDeltaRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
