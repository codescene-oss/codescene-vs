// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WorkspaceWatchCoordinatorGitIgnoreTests
    {
        [TestMethod]
        public async Task ApplyInventory_PrunesMonitor_SkipsIgnoredDirtyBuffers()
        {
            var repo = CreateGitRepo();
            var ignoredDirtyPath = Path.Combine(repo, "ignored-dirty.cs");
            var dirtyPath = Path.Combine(repo, "dirty.cs");
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                var git = new Mock<IGitService>();
                git.Setup(x => x.IsFileIgnored(ignoredDirtyPath)).Returns(true);
                git.Setup(x => x.IsFileIgnored(dirtyPath)).Returns(false);
                ISet<string> keep = null;
                host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
                {
                    new ReviewDocument { FilePath = ignoredDirtyPath, Content = "x", IsDirty = true },
                    new ReviewDocument { FilePath = dirtyPath, Content = "y", IsDirty = true },
                });
                host.Setup(x => x.PruneMonitor(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ISet<string>>()))
                    .Callback<IReadOnlyList<string>, ISet<string>>((_, paths) => keep = paths);

                using (var coordinator = new WorkspaceWatchCoordinator(
                    client.Object, pipeline.Object, null, host.Object, null, git.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                    client.Raise(
                        x => x.WatchInventoryChanged += null,
                        client.Object,
                        new WatchInventory { RepoRoot = repo, Files = new[] { "tracked.cs" } });

                    Assert.IsNotNull(keep);
                    Assert.IsTrue(keep.Any(path => path.EndsWith("dirty.cs", StringComparison.OrdinalIgnoreCase)));
                    Assert.IsFalse(keep.Any(path => path.EndsWith("ignored-dirty.cs", StringComparison.OrdinalIgnoreCase)));
                }
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task ServerRestart_DoesNotSeedIgnoredDirtyBuffers()
        {
            var repo = CreateGitRepo();
            var ignoredDirtyPath = Path.Combine(repo, "ignored-dirty.cs");
            File.WriteAllText(ignoredDirtyPath, "x");
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                var git = new Mock<IGitService>();
                git.Setup(x => x.IsFileIgnored(ignoredDirtyPath)).Returns(true);
                host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
                {
                    new ReviewDocument { FilePath = ignoredDirtyPath, Content = "x", IsDirty = true },
                });

                using (var coordinator = new WorkspaceWatchCoordinator(
                    client.Object, pipeline.Object, null, host.Object, null, git.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                    pipeline.Invocations.Clear();
                    client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Restart = true });
                    await Task.Delay(50);
                }

                pipeline.Verify(
                    x => x.SubmitBatchAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission[]>()),
                    Times.Never);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task AdmitsDeltaAsync_RejectsIgnoredDirtyPathNotInInventory()
        {
            var repo = CreateGitRepo();
            var ignoredDirtyPath = Path.Combine(repo, "ignored-dirty.cs");
            File.WriteAllText(ignoredDirtyPath, "x");
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                var git = new Mock<IGitService>();
                git.Setup(x => x.IsFileIgnored(ignoredDirtyPath)).Returns(true);
                host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
                {
                    new ReviewDocument { FilePath = ignoredDirtyPath, Content = "x", IsDirty = true },
                });

                using (var coordinator = new WorkspaceWatchCoordinator(
                    client.Object, pipeline.Object, null, host.Object, null, git.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                    client.Raise(
                        x => x.WatchInventoryChanged += null,
                        client.Object,
                        new WatchInventory { RepoRoot = repo, Files = new[] { "known.cs" } });

                    Assert.IsFalse(await coordinator.AdmitsDeltaAsync(repo, "ignored-dirty.cs"));
                }
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        private static string CreateGitRepo()
        {
            var path = Path.Combine(Path.GetTempPath(), "codescene-watch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            LibGit2Sharp.Repository.Init(path);
            return path;
        }
    }
}
