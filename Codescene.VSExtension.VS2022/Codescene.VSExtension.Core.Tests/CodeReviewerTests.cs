// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CodeReviewerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<IModelMapper> _mockMapper;
        private Mock<ICliExecutor> _mockExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockMapper = new Mock<IModelMapper>();
            _mockExecutor = new Mock<ICliExecutor>();
        }

        [TestMethod]
        public async Task ReviewWithDeltaAsync_WithPipelineOutsideRepository_ReturnsNull()
        {
            var pipeline = new Mock<IReviewPipeline>();
            var reviewer = new CodeReviewer(
                _mockLogger.Object,
                _mockMapper.Object,
                _mockExecutor.Object,
                pipeline.Object);

            var result = await reviewer.ReviewWithDeltaAsync(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "test.cs"),
                "content");

            Assert.IsNull(result.review);
            Assert.IsNull(result.delta);
            pipeline.Verify(
                x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission>()),
                Times.Never);
        }

        [TestMethod]
        public async Task ReviewWithDeltaAsync_WithPipelineReturnsMappedReviewAndDelta()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "code-reviewer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            LibGit2Sharp.Repository.Init(repoRoot);
            var path = Path.Combine(repoRoot, "test.cs");
            File.WriteAllText(path, "content");
            var cliReview = new CliReviewModel { RawScore = "raw" };
            var review = new FileReviewModel { FilePath = path, RawScore = "raw" };
            var delta = new DeltaResponseModel { ScoreChange = 1m };
            var pipeline = new Mock<IReviewPipeline>();
            pipeline.Setup(x => x.SubmitAsync(
                    It.IsAny<string>(),
                    It.IsAny<ReviewSubmission>()))
                .ReturnsAsync((cliReview, delta));
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(review);
            var reviewer = new CodeReviewer(
                _mockLogger.Object,
                _mockMapper.Object,
                _mockExecutor.Object,
                pipeline.Object);

            try
            {
                var result = await reviewer.ReviewWithDeltaAsync(path, "changed");

                Assert.IsNotNull(result.review);
                Assert.AreEqual(path, result.review.FilePath);
                Assert.AreSame(delta, result.delta);
                _mockMapper.Verify(x => x.Map(path, cliReview), Times.Once);
                pipeline.Verify(
                    x => x.SubmitAsync(
                        It.Is<string>(root => root.Equals(repoRoot, StringComparison.OrdinalIgnoreCase)),
                        It.Is<ReviewSubmission>(submission =>
                            submission.Document.FilePath == path &&
                            submission.Document.IsDirty &&
                            submission.RelPath == "test.cs" &&
                            submission.Content == "changed" &&
                            submission.UpdateDiagnosticsPane &&
                            submission.UpdateMonitor)),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }
    }
}
