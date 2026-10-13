// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ReviewPipelineOwnershipTests
    {
        private Mock<IIdeServerClient> _client;
        private PresentationEvents _events;
        private List<(string RepoRoot, IReadOnlyList<ReviewFile> Files)> _batches;
        private ReviewPipeline _pipeline;
        private string _repo;

        [TestInitialize]
        public void Setup()
        {
            _repo = Path.Combine(Path.GetTempPath(), "pipeline-own-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_repo, "src"));
            _client = new Mock<IIdeServerClient>();
            _events = new PresentationEvents();
            _batches = new List<(string, IReadOnlyList<ReviewFile>)>();
            _client.Setup(x => x.ReviewFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<ReviewFile>>()))
                .Callback<string, IReadOnlyList<ReviewFile>>((root, files) => _batches.Add((root, files.ToList())));
            _pipeline = new ReviewPipeline(_client.Object, _events, null, () => "review-1", null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _pipeline.Dispose();
            if (Directory.Exists(_repo))
            {
                Directory.Delete(_repo, true);
            }
        }

        [TestMethod]
        public void RestoreFromDiskIfBufferOwned_SubmitsDiskReviewAfterRemove()
        {
            var document = Sample();
            _ = _pipeline.SubmitAsync(_repo, Submission(document));

            _pipeline.RestoreFromDiskIfBufferOwned(_repo, document);

            Assert.HasCount(2, _batches);
            Assert.IsNull(_batches[1].Files[0].Content);
            Assert.HasCount(1, _events.Removed);
        }

        [TestMethod]
        public void SuspendBufferReviews_SkipsDiskRestoreOfOwnedBuffer()
        {
            var document = Sample();
            _ = _pipeline.SubmitAsync(_repo, Submission(document));
            _pipeline.SuspendBufferReviews();

            _pipeline.RestoreFromDiskIfBufferOwned(_repo, document);

            Assert.HasCount(1, _batches);
            Assert.HasCount(0, _events.Removed);
        }

        [TestMethod]
        public void ReleaseBufferMonitorOwnership_SkipsDiskRestore()
        {
            var document = Sample();
            _ = _pipeline.SubmitAsync(_repo, Submission(document));

            _pipeline.ReleaseBufferMonitorOwnership(_repo, document);
            _pipeline.RestoreFromDiskIfBufferOwned(_repo, document);

            Assert.HasCount(1, _batches);
            Assert.HasCount(0, _events.Removed);
        }

        [TestMethod]
        public async Task WatchDelta_OutsideInventory_IsIgnored()
        {
            _pipeline.SetWatchDeltaFilter((_, __) => Task.FromResult(false));
            _client.Raise(
                x => x.DeltaReceived += null,
                _client.Object,
                new DeltaNotification { RepoRoot = _repo, Path = "src/file.ts", Result = new DeltaResponseModel() });

            await Task.Delay(50);
            Assert.HasCount(0, _events.Deltas);
        }

        private ReviewDocument Sample()
        {
            return Document(Path.Combine(_repo, "src", "file.ts"), "const value = 1;");
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
                RelPath = RpcPath.RelativePosix(_repo, document.FilePath),
                Content = document.Content,
                UpdateDiagnosticsPane = true,
                UpdateMonitor = true,
            };
        }

        private sealed class PresentationEvents : IReviewPipelinePresentation
        {
            public List<PresentedReview> Reviews { get; } = new List<PresentedReview>();

            public List<PresentedDelta> Deltas { get; } = new List<PresentedDelta>();

            public List<ReviewDocument> Removed { get; } = new List<ReviewDocument>();

            public void ReviewStarted(ReviewDocument document)
            {
            }

            public void ReviewFinished(ReviewDocument document)
            {
            }

            public void DeltaStarted(ReviewDocument document)
            {
            }

            public void DeltaFinished(ReviewDocument document)
            {
            }

            public void PresentReview(PresentedReview review)
            {
                Reviews.Add(review);
            }

            public void PresentDelta(PresentedDelta delta)
            {
                Deltas.Add(delta);
            }

            public void Remove(ReviewDocument document)
            {
                Removed.Add(document);
            }

            public void Failed(Exception error)
            {
            }
        }
    }
}
