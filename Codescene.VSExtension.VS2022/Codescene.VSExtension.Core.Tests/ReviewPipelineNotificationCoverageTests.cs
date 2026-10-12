// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ReviewPipelineNotificationCoverageTests
    {
        private const string RepoRoot = "/repo";
        private Mock<IIdeServerClient> _client;
        private ReviewPipelinePresentationEvents _events;
        private ReviewPipeline _pipeline;

        [TestInitialize]
        public void Setup()
        {
            _client = new Mock<IIdeServerClient>();
            _events = new ReviewPipelinePresentationEvents();
            _pipeline = new ReviewPipeline(_client.Object, _events, null, () => "review-1", null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _pipeline.Dispose();
        }

        [TestMethod]
        public async Task WatchReview_WhenAutomaticAnalysisDisabled_IsIgnored()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(x => x.EnableAutomaticAnalysis).Returns(false);
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, true), () => "review-1", null, settings.Object);

            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification
                {
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = EmptyReview(GitBlobSha.FromUtf8(document.Content)),
                });

            await Task.Delay(50);
            Assert.IsEmpty(_events.Reviews);
        }

        [TestMethod]
        public async Task PendingDelta_WhenAutomaticAnalysisDisabled_PresentsWithoutMonitorUpdate()
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(x => x.EnableAutomaticAnalysis).Returns(false);
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, null, () => "review-1", null, settings.Object);
            var document = Document("/repo/src/file.ts", "const value = 1;");
            var pending = _pipeline.SubmitAsync(RepoRoot, Submission(document));
            RaiseReview("review-1", RepoRoot, "src/file.ts");
            RaiseDelta("review-1", RepoRoot, "src/file.ts", new DeltaResponseModel { NewGitBlobSha = GitBlobSha.FromUtf8(document.Content) });

            await pending;
            Assert.HasCount(1, _events.Deltas);
            Assert.IsFalse(_events.Deltas[0].UpdateMonitor);
        }

        [TestMethod]
        public async Task PendingReview_IgnoresMismatchedGitBlobSha()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            var pending = _pipeline.SubmitAsync(RepoRoot, Submission(document));
            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification
                {
                    Id = "review-1",
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = EmptyReview("stale-sha"),
                });

            var result = await pending;
            Assert.IsNull(result.Review);
            Assert.IsEmpty(_events.Reviews);
        }

        private void RaiseReview(string id, string repoRoot, string path)
        {
            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification { Id = id, RepoRoot = repoRoot, Path = path, Result = EmptyReview() });
        }

        private void RaiseDelta(string id, string repoRoot, string path, DeltaResponseModel result)
        {
            _client.Raise(
                x => x.DeltaReceived += null,
                _client.Object,
                new DeltaNotification { Id = id, RepoRoot = repoRoot, Path = path, Result = result });
        }

        private ReviewDocument Document(string filePath, string content)
        {
            return new ReviewDocument { FilePath = filePath, Content = content, IsDirty = true };
        }

        private ReviewSubmission Submission(ReviewDocument document)
        {
            return new ReviewSubmission
            {
                Document = document,
                RelPath = "src/file.ts",
                Content = document.Content,
                UpdateDiagnosticsPane = true,
                UpdateMonitor = true,
            };
        }

        private CliReviewModel EmptyReview(string gitBlobSha = null)
        {
            return new CliReviewModel
            {
                FileLevelCodeSmells = new List<CliCodeSmellModel>(),
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>(),
                RawScore = "raw",
                GitBlobSha = gitBlobSha,
            };
        }

        private IReviewPipelineFileAccess FileAccess(ReviewDocument document, bool visible)
        {
            var mock = new Mock<IReviewPipelineFileAccess>();
            mock.Setup(x => x.FindOpenDocument(It.IsAny<string>())).Returns(document);
            mock.Setup(x => x.OpenDocumentAsync(It.IsAny<string>())).ReturnsAsync(document);
            mock.Setup(x => x.ReadFileBytesAsync(It.IsAny<string>()))
                .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(document.Content ?? string.Empty));
            mock.Setup(x => x.IsVisible(It.IsAny<string>())).Returns(visible);
            return mock.Object;
        }
    }
}
