// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WorkspaceWatchCoordinatorTests
    {
        [TestMethod]
        public async Task Sync_SendsWatchFilesForResolvedRepo()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                }

                client.Verify(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.AtLeastOnce);
            }
            finally
            {
                if (Directory.Exists(repo))
                {
                    Directory.Delete(repo, true);
                }
            }
        }

        [TestMethod]
        public async Task ApplyInventory_PrunesMonitorAndPreservesDirtyBuffers()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                ISet<string> keep = null;
                var dirtyPath = Path.Combine(repo, "dirty.cs");
                host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
                {
                    new ReviewDocument { FilePath = dirtyPath, Content = "x", IsDirty = true },
                });
                host.Setup(x => x.PruneMonitor(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ISet<string>>()))
                    .Callback<IReadOnlyList<string>, ISet<string>>((_, paths) => keep = paths);

                using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                    client.Raise(
                        x => x.WatchInventoryChanged += null,
                        client.Object,
                        new WatchInventory { RepoRoot = repo, Files = new[] { "tracked.cs" } });

                    Assert.IsNotNull(keep);
                    Assert.IsTrue(keep.Any(path => path.EndsWith("tracked.cs", StringComparison.OrdinalIgnoreCase)));
                    Assert.IsTrue(keep.Any(path => path.EndsWith("dirty.cs", StringComparison.OrdinalIgnoreCase)));
                }
            }
            finally
            {
                if (Directory.Exists(repo))
                {
                    Directory.Delete(repo, true);
                }
            }
        }

        [TestMethod]
        public async Task Sync_SubdirectoryScope_SendsRelativeWatchPaths()
        {
            var repo = CreateGitRepo();
            var nested = Path.Combine(repo, "src");
            Directory.CreateDirectory(nested);
            try
            {
                IReadOnlyList<string> scopes = null;
                var client = new Mock<IIdeServerClient>();
                client.Setup(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                    .Callback<string, IReadOnlyList<string>>((_, paths) => scopes = paths);
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object))
                {
                    await coordinator.SyncAsync(new[] { nested });
                }

                Assert.IsNotNull(scopes);
                Assert.IsTrue(scopes.Any(path => path.Replace('\\', '/') == "src"));
            }
            finally
            {
                if (Directory.Exists(repo))
                {
                    Directory.Delete(repo, true);
                }
            }
        }

        [TestMethod]
        public async Task StopAll_SendsStopWatchFiles()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                    coordinator.StopAll();
                }

                client.Verify(x => x.StopWatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.AtLeastOnce);
                pipeline.Verify(x => x.Reset(), Times.AtLeastOnce);
                pipeline.Verify(x => x.SetActiveRepos(It.Is<IReadOnlyCollection<string>>(roots => roots.Count == 0)), Times.AtLeastOnce);
            }
            finally
            {
                if (Directory.Exists(repo))
                {
                    Directory.Delete(repo, true);
                }
            }
        }

        [TestMethod]
        public async Task ServerRestart_RestoresWatchesAndDirtyBuffers()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                var dirtyPath = Path.Combine(repo, "dirty.cs");
                File.WriteAllText(dirtyPath, "x");
                host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
                {
                    new ReviewDocument { FilePath = dirtyPath, Content = "x", IsDirty = true },
                });
                using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                    client.Invocations.Clear();
                    client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Restart = true });
                    await Task.Delay(50);
                }

                client.Verify(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.AtLeastOnce);
                pipeline.Verify(x => x.SubmitBatchAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission[]>()), Times.AtLeastOnce);
            }
            finally
            {
                if (Directory.Exists(repo))
                {
                    Directory.Delete(repo, true);
                }
            }
        }

        [TestMethod]
        public async Task StopAll_IgnoresLaterInventoryForClosedRepo()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                var pruneKeeps = new List<ISet<string>>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                host.Setup(x => x.PruneMonitor(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ISet<string>>()))
                    .Callback<IReadOnlyList<string>, ISet<string>>((_, paths) => pruneKeeps.Add(paths));
                using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object))
                {
                    await coordinator.SyncAsync(new[] { repo });
                    coordinator.StopAll();
                    var countAfterStop = pruneKeeps.Count;
                    client.Raise(
                        x => x.WatchInventoryChanged += null,
                        client.Object,
                        new WatchInventory { RepoRoot = repo, Files = new[] { "old.cs" } });

                    Assert.HasCount(countAfterStop, pruneKeeps);
                    Assert.IsEmpty(pruneKeeps[pruneKeeps.Count - 1]);
                }
            }
            finally
            {
                if (Directory.Exists(repo))
                {
                    Directory.Delete(repo, true);
                }
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
