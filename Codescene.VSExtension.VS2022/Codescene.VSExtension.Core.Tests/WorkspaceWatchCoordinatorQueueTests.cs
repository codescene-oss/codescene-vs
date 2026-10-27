// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WorkspaceWatchCoordinatorQueueTests
    {
        [TestMethod]
        public async Task QueueChanged_WithPendingWork_DoesNotIdlePrune()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                var pruneCount = 0;
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                host.Setup(x => x.PruneMonitor(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ISet<string>>()))
                    .Callback(() => pruneCount++);
                using var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);
                await coordinator.SyncAsync(new[] { repo });
                client.Raise(
                    x => x.WatchInventoryChanged += null,
                    client.Object,
                    new WatchInventory { RepoRoot = repo, Files = new[] { "tracked.cs" } });
                var afterInventory = pruneCount;

                client.Raise(
                    x => x.QueueChanged += null,
                    client.Object,
                    new ReviewQueue { Count = 2, Files = new[] { "a.cs", "b.cs" } });
                await Task.Delay(WorkspaceWatchCoordinator.QueueIdlePruneDelayMs + 150);

                Assert.AreEqual(afterInventory, pruneCount);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task AdmitsDeltaAsync_WhenInventoryNotLoadedYet_AdmitsWithoutRefresh()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);
                await coordinator.SyncAsync(new[] { repo });

                Assert.IsTrue(await coordinator.AdmitsDeltaAsync(repo, "new.cs"));
                client.Verify(x => x.GetWatchInventoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
