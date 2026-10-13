// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WorkspaceWatchScopeTests
    {
        [TestMethod]
        public async Task Sync_ParentAndSubdirectory_WatchesWholeRepo()
        {
            var repo = CreateGitRepo();
            var nested = Path.Combine(repo, "src");
            Directory.CreateDirectory(nested);
            try
            {
                IReadOnlyList<string> scopes = new[] { "unset" };
                var client = new Mock<IIdeServerClient>();
                client.Setup(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                    .Callback<string, IReadOnlyList<string>>((_, paths) => scopes = paths);
                using (var coordinator = CreateCoordinator(client))
                {
                    await coordinator.SyncAsync(new[] { repo, nested });
                }

                Assert.IsNull(scopes);
                client.Verify(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Once);
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task Sync_NestedForeignRepo_WatchesBothRepos()
        {
            var parent = CreateGitRepo();
            var nested = Path.Combine(parent, "vendor", "lib");
            Directory.CreateDirectory(nested);
            LibGit2Sharp.Repository.Init(nested);
            try
            {
                var roots = new List<string>();
                var client = new Mock<IIdeServerClient>();
                client.Setup(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                    .Callback<string, IReadOnlyList<string>>((root, _) => roots.Add(root));
                using (var coordinator = CreateCoordinator(client))
                {
                    await coordinator.SyncAsync(new[] { parent, nested });
                }

                Assert.HasCount(2, roots);
                Assert.IsTrue(roots.Any(root => root.Equals(parent, StringComparison.OrdinalIgnoreCase)));
                Assert.IsTrue(roots.Any(root => root.Equals(nested, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }

        [TestMethod]
        public async Task Sync_ScopeChange_StopsBeforeStarting()
        {
            var repo = CreateGitRepo();
            var src = Path.Combine(repo, "src");
            var lib = Path.Combine(repo, "lib");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(lib);
            try
            {
                var order = new List<string>();
                var client = new Mock<IIdeServerClient>();
                client.Setup(x => x.StopWatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                    .Callback(() => order.Add("stop"));
                client.Setup(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                    .Callback(() => order.Add("watch"));
                using (var coordinator = CreateCoordinator(client))
                {
                    await coordinator.SyncAsync(new[] { src });
                    order.Clear();
                    await coordinator.SyncAsync(new[] { lib });
                    Assert.HasCount(2, order);
                    Assert.AreEqual("stop", order[0]);
                    Assert.AreEqual("watch", order[1]);
                }
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        [TestMethod]
        public async Task Sync_SessionCapturedBeforeStop_DoesNotWatchAgain()
        {
            var repo = CreateGitRepo();
            try
            {
                var watches = 0;
                var client = new Mock<IIdeServerClient>();
                client.Setup(x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                    .Callback(() => watches++);
                using (var coordinator = CreateCoordinator(client))
                {
                    var session = coordinator.WatchSession;
                    await coordinator.SyncAsync(new[] { repo }, session);
                    coordinator.StopAll();
                    await coordinator.SyncAsync(new[] { repo }, session);
                    Assert.AreEqual(1, watches);

                    await coordinator.SyncAsync(new[] { repo });
                    Assert.AreEqual(2, watches);
                }
            }
            finally
            {
                Directory.Delete(repo, true);
            }
        }

        private static WorkspaceWatchCoordinator CreateCoordinator(Mock<IIdeServerClient> client)
        {
            var pipeline = new Mock<IReviewPipeline>();
            var host = new Mock<IWorkspaceWatchHost>();
            host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
            return new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object);
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
