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
    public class CodeReviewerPipelineRestoreTests
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
        public async Task ReviewWithDeltaAsync_WhenContentMatchesDisk_RestoresBufferOwnedMonitor()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "code-reviewer-restore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            LibGit2Sharp.Repository.Init(repoRoot);
            var path = Path.Combine(repoRoot, "test.cs");
            File.WriteAllText(path, "content");
            var cliReview = new CliReviewModel { RawScore = "raw" };
            var review = new FileReviewModel { FilePath = path, RawScore = "raw" };
            var pipeline = new Mock<IReviewPipeline>();
            pipeline.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission>()))
                .ReturnsAsync((cliReview, (DeltaResponseModel)null));
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(review);
            var reviewer = new CodeReviewer(
                _mockLogger.Object,
                _mockMapper.Object,
                _mockExecutor.Object,
                pipeline.Object);

            try
            {
                await reviewer.ReviewWithDeltaAsync(path, "content");

                pipeline.Verify(
                    x => x.RestoreFromDiskIfBufferOwned(
                        It.Is<string>(root => root.Equals(repoRoot, StringComparison.OrdinalIgnoreCase)),
                        It.Is<ReviewDocument>(document => document.FilePath == path)),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }

        [TestMethod]
        public async Task ReviewWithDeltaAsync_WhenContentDiffersFromDisk_DoesNotRestoreBufferOwnedMonitor()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "code-reviewer-dirty-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            LibGit2Sharp.Repository.Init(repoRoot);
            var path = Path.Combine(repoRoot, "test.cs");
            File.WriteAllText(path, "on disk");
            var cliReview = new CliReviewModel { RawScore = "raw" };
            var review = new FileReviewModel { FilePath = path, RawScore = "raw" };
            var pipeline = new Mock<IReviewPipeline>();
            pipeline.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission>()))
                .ReturnsAsync((cliReview, (DeltaResponseModel)null));
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(review);
            var reviewer = new CodeReviewer(
                _mockLogger.Object,
                _mockMapper.Object,
                _mockExecutor.Object,
                pipeline.Object);

            try
            {
                await reviewer.ReviewWithDeltaAsync(path, "changed");

                pipeline.Verify(
                    x => x.RestoreFromDiskIfBufferOwned(It.IsAny<string>(), It.IsAny<ReviewDocument>()),
                    Times.Never);
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }
    }
}
