// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Delta;
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

        [TestMethod]
        public async Task Sync_RemovesRepositoryDroppedFromWorkspace()
        {
            var firstRepo = CreateGitRepo();
            var secondRepo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);
                await coordinator.SyncAsync(new[] { firstRepo, secondRepo });
                client.Invocations.Clear();

                await coordinator.SyncAsync(new[] { secondRepo });

                client.Verify(
                    x => x.StopWatchFiles(
                        It.Is<string>(path => path.Equals(firstRepo, StringComparison.OrdinalIgnoreCase)),
                        It.IsAny<IReadOnlyList<string>>()),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(firstRepo, true);
                Directory.Delete(secondRepo, true);
            }
        }

        [TestMethod]
        public async Task Sync_RepeatedScope_SeedsWithoutRewatching()
        {
            var repo = CreateGitRepo();
            var filePath = Path.Combine(repo, "dirty.cs");
            File.WriteAllText(filePath, "x");
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
                {
                    new ReviewDocument { FilePath = filePath, Content = "x", IsDirty = true },
                });
                using var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);

                await coordinator.SyncAsync(new[] { repo });
                await coordinator.SyncAsync(new[] { repo });

                client.Verify(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Once);
                pipeline.Verify(x => x.SubmitBatchAsync(It.IsAny<string>(), It.IsAny<ReviewSubmission[]>()), Times.Exactly(2));
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task GetInventoryFiles_ReturnsCurrentInventory()
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
                Assert.IsEmpty(coordinator.GetInventoryFiles(repo));

                client.Raise(
                    x => x.WatchInventoryChanged += null,
                    client.Object,
                    new WatchInventory { RepoRoot = repo, Files = new[] { "tracked.cs" } });

                Assert.Contains("tracked.cs", coordinator.GetInventoryFiles(repo));
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task DeltaReceived_UnknownPath_RefreshesInventory()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                client.Setup(x => x.GetWatchInventoryAsync(repo, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new WatchInventory { RepoRoot = repo, Files = new[] { "known.cs", "new.cs" } });
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);
                await coordinator.SyncAsync(new[] { repo });
                client.Raise(
                    x => x.WatchInventoryChanged += null,
                    client.Object,
                    new WatchInventory { RepoRoot = repo, Files = new[] { "known.cs" } });

                client.Raise(
                    x => x.DeltaReceived += null,
                    client.Object,
                    new DeltaNotification
                    {
                        RepoRoot = repo,
                        Path = "new.cs",
                        Result = new DeltaResponseModel(),
                    });

                await WaitUntilAsync(() => coordinator.GetInventoryFiles(repo).Contains("new.cs"));
                client.Verify(x => x.GetWatchInventoryAsync(repo, It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task DeltaReceived_TrackedOrDirtyPath_DoesNotRefreshInventory()
        {
            var repo = CreateGitRepo();
            var dirtyPath = Path.Combine(repo, "dirty.cs");
            File.WriteAllText(dirtyPath, "x");
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
                {
                    new ReviewDocument { FilePath = dirtyPath, Content = "x", IsDirty = true },
                });
                using var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);
                await coordinator.SyncAsync(new[] { repo });
                client.Raise(
                    x => x.WatchInventoryChanged += null,
                    client.Object,
                    new WatchInventory { RepoRoot = repo, Files = new[] { "known.cs" } });

                client.Raise(
                    x => x.DeltaReceived += null,
                    client.Object,
                    new DeltaNotification { RepoRoot = repo, Path = "known.cs", Result = new DeltaResponseModel() });
                client.Raise(
                    x => x.DeltaReceived += null,
                    client.Object,
                    new DeltaNotification { RepoRoot = repo, Path = "dirty.cs", Result = new DeltaResponseModel() });
                await Task.Delay(WorkspaceWatchCoordinator.InventoryRefreshDelayMs + 100);

                client.Verify(x => x.GetWatchInventoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task InvalidNotificationsAndPostDisposeSync_AreIgnored()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);
                await coordinator.SyncAsync(new[] { repo });
                client.Invocations.Clear();

                client.Raise(x => x.WatchInventoryChanged += null, client.Object, null);
                client.Raise(x => x.DeltaReceived += null, client.Object, null);
                client.Raise(x => x.ServerStarted += null, client.Object, null);
                client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Restart = false });
                coordinator.Dispose();
                coordinator.Dispose();
                await coordinator.SyncAsync(new[] { repo });

                client.Verify(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
                client.Verify(x => x.GetWatchInventoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task DeltaReceived_BeforeInventory_IsIgnored()
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

                client.Raise(
                    x => x.DeltaReceived += null,
                    client.Object,
                    new DeltaNotification { RepoRoot = repo, Path = "new.cs", Result = new DeltaResponseModel() });
                await Task.Delay(WorkspaceWatchCoordinator.InventoryRefreshDelayMs + 100);

                client.Verify(
                    x => x.GetWatchInventoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task DeltaReceived_RepeatedUnknownPath_LogsRefreshFailure()
        {
            var repo = CreateGitRepo();
            try
            {
                var client = new Mock<IIdeServerClient>();
                client.Setup(x => x.GetWatchInventoryAsync(repo, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("inventory failed"));
                var pipeline = new Mock<IReviewPipeline>();
                var logger = new Mock<Codescene.VSExtension.Core.Interfaces.ILogger>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using var coordinator = new WorkspaceWatchCoordinator(
                    client.Object,
                    pipeline.Object,
                    logger.Object,
                    host.Object);
                await coordinator.SyncAsync(new[] { repo });
                client.Raise(
                    x => x.WatchInventoryChanged += null,
                    client.Object,
                    new WatchInventory { RepoRoot = repo, Files = Array.Empty<string>() });
                var delta = new DeltaNotification
                {
                    RepoRoot = repo,
                    Path = "new.cs",
                    Result = new DeltaResponseModel(),
                };

                client.Raise(x => x.DeltaReceived += null, client.Object, delta);
                client.Raise(x => x.DeltaReceived += null, client.Object, delta);

                await Task.Delay(WorkspaceWatchCoordinator.InventoryRefreshDelayMs + 200);
                logger.Verify(
                    x => x.Debug(It.Is<string>(message => message.Contains("inventory refresh skipped"))),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task StopAll_CancelsPendingInventoryRefresh()
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
                client.Raise(
                    x => x.WatchInventoryChanged += null,
                    client.Object,
                    new WatchInventory { RepoRoot = repo, Files = Array.Empty<string>() });
                client.Raise(
                    x => x.DeltaReceived += null,
                    client.Object,
                    new DeltaNotification { RepoRoot = repo, Path = "new.cs", Result = new DeltaResponseModel() });

                coordinator.StopAll();
                await Task.Delay(WorkspaceWatchCoordinator.InventoryRefreshDelayMs + 100);

                client.Verify(
                    x => x.GetWatchInventoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException("Timed out waiting for workspace inventory refresh.");
                }

                await Task.Delay(25);
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
