// Copyright (c) CodeScene. All rights reserved.

using System.Text;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Cli.Rpc;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WorkspaceReviewListenerTests
    {
        private Mock<ILogger> _logger;
        private Mock<IIdeServerClient> _client;
        private WorkspaceReviewListener _listener;
        private string _tempDir;

        [TestInitialize]
        public void Setup()
        {
            _logger = new Mock<ILogger>();
            _client = new Mock<IIdeServerClient>();
            _tempDir = Path.Combine(Path.GetTempPath(), "cs-review-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            new ReviewCacheService().Clear();
            new DeltaCacheService().Clear();
            _listener = new WorkspaceReviewListener(_logger.Object, new ModelMapper());
            _listener.Attach(_client.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _listener.Detach();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [TestMethod]
        public void BufferReview_AppliesMatchingIdAndSha()
        {
            FileReviewAppliedEventArgs applied = null;
            _listener.ReviewApplied += (s, e) => applied = e;
            var content = "class A {}";
            var id = _listener.SubmitBufferReview(_tempDir, "a.cs", Path.Combine(_tempDir, "a.cs"), content);

            _client.Raise(
                c => c.FileReviewReceived += null,
                _client.Object,
                new FileReviewNotification
                {
                    Id = id,
                    Path = "a.cs",
                    RepoRoot = _tempDir,
                    Result = new CliReviewModel
                    {
                        Score = 8.5f,
                        RawScore = "raw",
                        GitBlobSha = GitBlobSha.FromUtf8(content),
                        FileLevelCodeSmells = new List<CliCodeSmellModel>(),
                        FunctionLevelCodeSmells = new List<CliReviewFunctionModel>(),
                    },
                });

            Assert.IsNotNull(applied);
            Assert.AreEqual(id, applied.Id);
            Assert.AreEqual(8.5f, applied.Review.Score);
        }

        [TestMethod]
        public void WatchNotification_WithoutId_IsAppliedWhenShaMatchesDisk()
        {
            var filePath = Path.Combine(_tempDir, "watched.cs");
            var content = "class Watched {}";
            File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes(content));
            var sha = GitBlobSha.FromBytes(File.ReadAllBytes(filePath));
            FileReviewAppliedEventArgs applied = null;
            _listener.ReviewApplied += (s, e) => applied = e;

            _client.Raise(
                c => c.FileReviewReceived += null,
                _client.Object,
                new FileReviewNotification
                {
                    Path = "watched.cs",
                    RepoRoot = _tempDir,
                    Result = new CliReviewModel
                    {
                        Score = 7.1f,
                        RawScore = "raw",
                        GitBlobSha = sha,
                        FileLevelCodeSmells = new List<CliCodeSmellModel>(),
                        FunctionLevelCodeSmells = new List<CliReviewFunctionModel>(),
                    },
                });

            Assert.IsNotNull(applied);
            Assert.IsNull(applied.Id);
            Assert.AreEqual(Path.GetFullPath(filePath), applied.AbsolutePath);
            Assert.IsNotNull(new ReviewCacheService().Get(new Core.Models.Cache.Review.ReviewCacheQuery(content, Path.GetFullPath(filePath))));
        }

        [TestMethod]
        public void UnknownId_IsIgnored()
        {
            var applied = false;
            _listener.ReviewApplied += (s, e) => applied = true;

            _client.Raise(
                c => c.FileReviewReceived += null,
                _client.Object,
                new FileReviewNotification
                {
                    Id = "unknown",
                    Path = "a.cs",
                    RepoRoot = _tempDir,
                    Result = new CliReviewModel { Score = 1, GitBlobSha = "x" },
                });

            Assert.IsFalse(applied);
        }
    }
}
