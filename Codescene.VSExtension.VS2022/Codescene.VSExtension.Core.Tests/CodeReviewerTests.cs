// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
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

        [TestMethod]
        public async Task ReviewWithDeltaAsync_WhenPipelineReturnsNull_ReturnsNull()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "code-reviewer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            LibGit2Sharp.Repository.Init(repoRoot);
            var path = Path.Combine(repoRoot, "test.cs");
            File.WriteAllText(path, "same");
            var pipeline = new Mock<IReviewPipeline>();
            pipeline.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission>()))
                .ReturnsAsync(((CliReviewModel)null, (DeltaResponseModel)null));
            var reviewer = new CodeReviewer(_mockLogger.Object, _mockMapper.Object, _mockExecutor.Object, pipeline.Object);

            try
            {
                var result = await reviewer.ReviewWithDeltaAsync(path, "same");
                Assert.IsNull(result.review);
                Assert.IsNull(result.delta);
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }

        [TestMethod]
        public async Task ReviewWithDeltaAsync_WhenRawScoreMissing_ReturnsReviewWithoutDelta()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "code-reviewer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            LibGit2Sharp.Repository.Init(repoRoot);
            var path = Path.Combine(repoRoot, "test.cs");
            File.WriteAllText(path, "content");
            var cliReview = new CliReviewModel();
            var review = new FileReviewModel { FilePath = path };
            var pipeline = new Mock<IReviewPipeline>();
            pipeline.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission>()))
                .ReturnsAsync((cliReview, new DeltaResponseModel()));
            _mockMapper.Setup(x => x.Map(path, cliReview)).Returns(review);
            var reviewer = new CodeReviewer(_mockLogger.Object, _mockMapper.Object, _mockExecutor.Object, pipeline.Object);

            try
            {
                var result = await reviewer.ReviewWithDeltaAsync(path, "changed");
                Assert.AreSame(review, result.review);
                Assert.IsNull(result.delta);
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }

        [TestMethod]
        public async Task ReviewWithDeltaAsync_WhenAutomaticAnalysisDisabled_SkipsMonitorUpdate()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "code-reviewer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            LibGit2Sharp.Repository.Init(repoRoot);
            var path = Path.Combine(repoRoot, "test.cs");
            File.WriteAllText(path, "disk");
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(x => x.EnableAutomaticAnalysis).Returns(false);
            var pipeline = new Mock<IReviewPipeline>();
            pipeline.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission>()))
                .ReturnsAsync((new CliReviewModel { RawScore = "raw" }, (DeltaResponseModel)null));
            _mockMapper.Setup(x => x.Map(path, It.IsAny<CliReviewModel>())).Returns(new FileReviewModel());
            var reviewer = new CodeReviewer(
                _mockLogger.Object,
                _mockMapper.Object,
                _mockExecutor.Object,
                pipeline.Object,
                settings: settings.Object);

            try
            {
                await reviewer.ReviewWithDeltaAsync(path, "buffer");
                pipeline.Verify(
                    x => x.SubmitAsync(
                        It.IsAny<string>(),
                        It.Is<ReviewSubmission>(submission => !submission.UpdateMonitor)),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }

        [TestMethod]
        public async Task ReviewWithDeltaAsync_EnrichesDeltaWithRefactorableFunctions()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "code-reviewer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            LibGit2Sharp.Repository.Init(repoRoot);
            var path = Path.Combine(repoRoot, "test.cs");
            File.WriteAllText(path, "content");
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel { Function = new FunctionInfoModel { Name = "Fn" } },
                },
            };
            var pipeline = new Mock<IReviewPipeline>();
            pipeline.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission>()))
                .ReturnsAsync((new CliReviewModel { RawScore = "raw" }, delta));
            _mockMapper.Setup(x => x.Map(path, It.IsAny<CliReviewModel>())).Returns(new FileReviewModel { RawScore = "raw" });
            var preflight = new Mock<IPreflightManager>();
            preflight.Setup(x => x.GetPreflightResponseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PreFlightResponseModel());
            _mockExecutor.Setup(x => x.FnsToRefactorFromDeltaAsync(path, "changed", delta, It.IsAny<PreFlightResponseModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FnToRefactorModel> { new FnToRefactorModel { Name = "Fn" } });
            var reviewer = new CodeReviewer(
                _mockLogger.Object,
                _mockMapper.Object,
                _mockExecutor.Object,
                pipeline.Object,
                preflight.Object);

            try
            {
                var result = await reviewer.ReviewWithDeltaAsync(path, "changed");
                Assert.IsNotNull(result.delta?.FunctionLevelFindings[0].RefactorableFn);
                Assert.AreEqual("Fn", result.delta.FunctionLevelFindings[0].RefactorableFn.Name);
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }
    }
}
