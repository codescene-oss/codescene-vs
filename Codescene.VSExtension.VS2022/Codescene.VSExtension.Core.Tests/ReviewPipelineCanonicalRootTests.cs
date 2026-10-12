// Copyright (c) CodeScene. All rights reserved.

using System.Runtime.InteropServices;
using System.Text;
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
    public class ReviewPipelineCanonicalRootTests
    {
        private Mock<IIdeServerClient> _client;
        private ReviewPipelinePresentationEvents _events;
        private int _nextId;
        private ReviewPipeline _pipeline;

        [TestInitialize]
        public void Setup()
        {
            _client = new Mock<IIdeServerClient>();
            _events = new ReviewPipelinePresentationEvents();
            _nextId = 1;
            _client.Setup(x => x.ReviewFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<ReviewFile>>()));
            _pipeline = new ReviewPipeline(_client.Object, _events, null, () => "review-" + _nextId++, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _pipeline.Dispose();
        }

        [TestMethod]
        public async Task Submit_AcceptsCanonicalRepoRootFromNotification()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive();
                return;
            }

            var localRoot = Path.Combine(Path.GetTempPath(), "pipeline-canonical-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(localRoot);
            var shortRoot = GetShortPath(localRoot);
            if (RpcPath.PathsEqual(localRoot, shortRoot))
            {
                Directory.Delete(localRoot, true);
                Assert.Inconclusive("Short path alias unavailable for temp directory.");
                return;
            }

            var srcDir = Path.Combine(localRoot, "src");
            Directory.CreateDirectory(srcDir);
            var document = new ReviewDocument
            {
                FilePath = Path.Combine(srcDir, "file.ts"),
                Content = "const value = 1;",
                IsDirty = true,
            };
            var reviewPromise = _pipeline.SubmitAsync(
                localRoot,
                new ReviewSubmission
                {
                    Document = document,
                    RelPath = "src/file.ts",
                    Content = document.Content,
                    UpdateDiagnosticsPane = true,
                    UpdateMonitor = true,
                });

            _client.Raise(
                x => x.ReviewReceived += null,
                _client.Object,
                new ReviewNotification
                {
                    Id = "review-1",
                    RepoRoot = shortRoot,
                    Path = "src/file.ts",
                    Result = new CliReviewModel
                    {
                        FileLevelCodeSmells = new List<CliCodeSmellModel>(),
                        FunctionLevelCodeSmells = new List<CliReviewFunctionModel>(),
                        RawScore = "raw",
                    },
                });
            _client.Raise(
                x => x.DeltaReceived += null,
                _client.Object,
                new DeltaNotification { Id = "review-1", RepoRoot = shortRoot, Path = "src/file.ts", Result = null });

            try
            {
                var result = await reviewPromise;
                Assert.IsNotNull(result.Review);
                Assert.HasCount(1, _events.Reviews);
                Assert.HasCount(1, _events.Deltas);
            }
            finally
            {
                Directory.Delete(localRoot, true);
            }
        }

        private static string GetShortPath(string longPath)
        {
            var buffer = new StringBuilder(512);
            var length = GetShortPathName(longPath, buffer, (uint)buffer.Capacity);
            if (length == 0 || length >= buffer.Capacity)
            {
                return longPath;
            }

            return buffer.ToString();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);
    }
}
