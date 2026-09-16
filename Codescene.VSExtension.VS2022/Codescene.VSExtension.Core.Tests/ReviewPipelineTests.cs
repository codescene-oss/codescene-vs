// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ReviewPipelineTests
    {
        private const string RepoRoot = "/repo";
        private Mock<IIdeServerClient> _client;
        private PresentationEvents _events;
        private List<(string RepoRoot, IReadOnlyList<ReviewFile> Files)> _batches;
        private int _nextId;
        private ReviewPipeline _pipeline;

        [TestInitialize]
        public void Setup()
        {
            _client = new Mock<IIdeServerClient>();
            _events = new PresentationEvents();
            _batches = new List<(string, IReadOnlyList<ReviewFile>)>();
            _nextId = 1;
            _client.Setup(x => x.ReviewFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<ReviewFile>>()))
                .Callback<string, IReadOnlyList<ReviewFile>>((root, files) => _batches.Add((root, files.ToList())));
            _pipeline = new ReviewPipeline(_client.Object, _events, null, () => "review-" + _nextId++, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _pipeline.Dispose();
        }

        [TestMethod]
        public void SubmitBatch_DeduplicatesRepoPathAndContent()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _ = _pipeline.SubmitBatchAsync(RepoRoot, new[] { Submission(document), Submission(document) });

            Assert.HasCount(1, _batches);
            Assert.HasCount(1, _batches[0].Files);
            Assert.AreEqual("src/file.ts", _batches[0].Files[0].RelPath);
        }

        [TestMethod]
        public async Task Submit_DoesNotResubmitCompletedUnchangedContent()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            var first = _pipeline.SubmitAsync(RepoRoot, Submission(document));
            CompleteReview("review-1");
            await first;

            await _pipeline.SubmitAsync(RepoRoot, Submission(document));

            Assert.HasCount(1, _batches);
        }

        [TestMethod]
        public async Task Submit_OnlyPresentsLatestGeneration()
        {
            var oldDocument = Document("/repo/src/file.ts", "const value = 1;");
            var newDocument = Document("/repo/src/file.ts", "const value = 2;");
            var oldPromise = _pipeline.SubmitAsync(RepoRoot, Submission(oldDocument));
            var newPromise = _pipeline.SubmitAsync(RepoRoot, Submission(newDocument));

            RaiseReview("review-1", RepoRoot, "src/file.ts");
            RaiseDelta("review-1", RepoRoot, "src/file.ts", null);
            RaiseReview("review-2", RepoRoot, "src/file.ts");
            RaiseDelta("review-2", RepoRoot, "src/file.ts", null);

            await Task.WhenAll(oldPromise, newPromise);
            Assert.HasCount(1, _events.Reviews);
            Assert.AreEqual("const value = 2;", _events.Reviews[0].Document.Content);
            Assert.HasCount(1, _events.Deltas);
            Assert.IsNull(_events.Deltas[0].Result);
        }

        [TestMethod]
        public async Task Remove_TombstonesPathAndIgnoresLateResponses()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            var reviewPromise = _pipeline.SubmitAsync(RepoRoot, Submission(document));
            _pipeline.Remove(RepoRoot, document);

            RaiseReview("review-1", RepoRoot, "src/file.ts");
            RaiseDelta("review-1", RepoRoot, "src/file.ts", null);

            await reviewPromise;
            Assert.HasCount(1, _events.Removed);
            Assert.HasCount(0, _events.Reviews);
            Assert.HasCount(0, _events.Deltas);
        }

        [TestMethod]
        public async Task Submit_IgnoresNotificationsFromAnotherRepository()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            var reviewPromise = _pipeline.SubmitAsync(RepoRoot, Submission(document));

            RaiseReview("review-1", "/other", "src/file.ts");
            RaiseDelta("review-1", "/other", "src/file.ts", null);

            await reviewPromise;
            Assert.HasCount(0, _events.Reviews);
            Assert.HasCount(0, _events.Deltas);
        }

        [TestMethod]
        public async Task Submit_KeepsIdenticalRelativePathsIsolatedByRepository()
        {
            var firstDocument = Document("/repo/src/file.ts", "const value = 1;");
            var secondDocument = Document("/other/src/file.ts", "const value = 2;");
            var firstPromise = _pipeline.SubmitAsync("/repo", Submission(firstDocument));
            var secondPromise = _pipeline.SubmitAsync("/other", Submission(secondDocument));

            RaiseReview("review-1", "/repo", "src/file.ts");
            RaiseDelta("review-1", "/repo", "src/file.ts", null);
            RaiseReview("review-2", "/other", "src/file.ts");
            RaiseDelta("review-2", "/other", "src/file.ts", null);

            await Task.WhenAll(firstPromise, secondPromise);
            CollectionAssert.AreEqual(
                new[] { firstDocument.FilePath, secondDocument.FilePath },
                _events.Reviews.Select(review => review.Document.FilePath).ToArray());
        }

        [TestMethod]
        public async Task Submit_ReportsReviewFailures()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            var reviewPromise = _pipeline.SubmitAsync(RepoRoot, Submission(document));

            _client.Raise(
                x => x.ReviewFailed += null,
                _client.Object,
                new ReviewFailedNotification { Id = "review-1", RepoRoot = RepoRoot, Path = "src/file.ts", Message = "Review failed" });

            var exception = await Assert.ThrowsAsync<Exception>(() => reviewPromise);
            Assert.AreEqual("Review failed", exception.Message);
            Assert.HasCount(1, _events.Failures);
        }

        [TestMethod]
        public async Task Submit_AcceptsWindowsPathSeparatorAndDriveCaseDifferences()
        {
            const string windowsRoot = @"c:\Git\codescene";
            var document = Document(@"c:\Git\codescene\CSharp\Example.cs", "class Example {}");
            var reviewPromise = _pipeline.SubmitAsync(
                windowsRoot,
                new ReviewSubmission
                {
                    Document = document,
                    RelPath = @"CSharp\Example.cs",
                    Content = document.Content,
                    UpdateDiagnosticsPane = true,
                    UpdateMonitor = true,
                });

            CompleteReview("review-1", @"C:\Git\codescene", "CSharp/Example.cs");

            var result = await reviewPromise;
            Assert.IsNotNull(result.Review);
            Assert.HasCount(1, _events.Reviews);
            Assert.HasCount(1, _events.Deltas);
        }

        [TestMethod]
        public void SubmitBatch_SubmitsDiskFilesWithoutIdOrContent()
        {
            var submissions = new[]
            {
                new ReviewSubmission
                {
                    RelPath = "src/file.ts",
                    UpdateDiagnosticsPane = false,
                    UpdateMonitor = true,
                },
            };
            _ = _pipeline.SubmitBatchAsync(RepoRoot, submissions);

            Assert.HasCount(1, _batches);
            Assert.IsNull(_batches[0].Files[0].Id);
            Assert.IsNull(_batches[0].Files[0].Content);
            Assert.AreEqual("src/file.ts", _batches[0].Files[0].RelPath);
        }

        [TestMethod]
        public async Task WatchReview_PresentsWhenGitBlobShaMatches()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, true), () => "review-1", null);
            var sha = GitBlobSha.FromUtf8(document.Content);

            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification { RepoRoot = RepoRoot, Path = "src/file.ts", Result = EmptyReview(sha) });
            _client.Raise(
                x => x.DeltaReceived += null,
                _client.Object,
                new DeltaNotification
                {
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = new DeltaResponseModel { NewGitBlobSha = sha },
                });

            await WaitUntil(() => _events.Reviews.Count == 1 && _events.Deltas.Count == 1);
            Assert.IsTrue(_events.Reviews[0].UpdateDiagnosticsPane);
            Assert.IsTrue(_events.Reviews[0].UpdateMonitor);
            Assert.AreEqual(sha, _events.Deltas[0].Result.NewGitBlobSha);
        }

        [TestMethod]
        public async Task WatchReview_DiscardsStaleGitBlobSha()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, false), () => "review-1", null);

            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification { RepoRoot = RepoRoot, Path = "src/file.ts", Result = EmptyReview("stale-sha") });

            await Task.Delay(50);
            Assert.HasCount(0, _events.Reviews);
        }

        [TestMethod]
        public async Task WatchReview_ServesDiagnosticsOnlySubmissionWithoutResubmitting()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, false), () => "review-1", null);
            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification
                {
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = EmptyReview(GitBlobSha.FromUtf8(document.Content)),
                });
            await WaitUntil(() => _events.Reviews.Count == 1);

            var submission = Submission(document);
            submission.UpdateMonitor = false;
            var review = await _pipeline.SubmitAsync(RepoRoot, submission);

            Assert.HasCount(0, _batches);
            Assert.HasCount(2, _events.Reviews);
            Assert.IsNotNull(review.Review);
        }

        [TestMethod]
        public async Task WatchReview_StillSubmitsWhenMonitorNeedsDelta()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, false), () => "review-1", null);
            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification
                {
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = EmptyReview(GitBlobSha.FromUtf8(document.Content)),
                });
            await WaitUntil(() => _events.Reviews.Count == 1);

            _ = _pipeline.SubmitAsync(RepoRoot, Submission(document));

            Assert.HasCount(1, _batches);
        }

        [TestMethod]
        public async Task WatchReview_StillSubmitsWhenBufferContentDiffers()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, false), () => "review-1", null);
            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification
                {
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = EmptyReview(GitBlobSha.FromUtf8(document.Content)),
                });
            await WaitUntil(() => _events.Reviews.Count == 1);

            var edited = Document("/repo/src/file.ts", "const value = 2;");
            var submission = Submission(edited);
            submission.UpdateMonitor = false;
            _ = _pipeline.SubmitAsync(RepoRoot, submission);

            Assert.HasCount(1, _batches);
        }

        [TestMethod]
        public async Task WatchReview_StillSubmitsAfterInvalidate()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, false), () => "review-1", null);
            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification
                {
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = EmptyReview(GitBlobSha.FromUtf8(document.Content)),
                });
            await WaitUntil(() => _events.Reviews.Count == 1);

            _pipeline.Invalidate();
            var submission = Submission(document);
            submission.UpdateMonitor = false;
            _ = _pipeline.SubmitAsync(RepoRoot, submission);

            Assert.HasCount(1, _batches);
        }

        [TestMethod]
        public async Task Reset_DropsLaterWatchDelta()
        {
            var document = Document("/repo/src/file.ts", "const value = 1;");
            _pipeline.Dispose();
            _pipeline = new ReviewPipeline(_client.Object, _events, FileAccess(document, false), () => "review-1", null);
            _pipeline.SetActiveRepos(Array.Empty<string>());
            _pipeline.Reset();
            _client.Raise(
                x => x.DeltaReceived += null,
                _client.Object,
                new DeltaNotification
                {
                    RepoRoot = RepoRoot,
                    Path = "src/file.ts",
                    Result = new DeltaResponseModel { OldScore = 10, NewScore = 9 },
                });
            await Task.Delay(50);

            Assert.IsEmpty(_events.Deltas);
        }

        private void CompleteReview(string id, string repoRoot = RepoRoot, string path = "src/file.ts")
        {
            RaiseReview(id, repoRoot, path);
            RaiseDelta(id, repoRoot, path, null);
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

        private async Task WaitUntil(Func<bool> predicate)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (predicate())
                {
                    return;
                }

                await Task.Delay(10);
            }

            throw new TimeoutException("timed out waiting for pipeline presentation");
        }

        private sealed class PresentationEvents : IReviewPipelinePresentation
        {
            public List<PresentedReview> Reviews { get; } = new List<PresentedReview>();

            public List<PresentedDelta> Deltas { get; } = new List<PresentedDelta>();

            public List<ReviewDocument> Removed { get; } = new List<ReviewDocument>();

            public List<Exception> Failures { get; } = new List<Exception>();

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
                Failures.Add(error);
            }
        }
    }
}
