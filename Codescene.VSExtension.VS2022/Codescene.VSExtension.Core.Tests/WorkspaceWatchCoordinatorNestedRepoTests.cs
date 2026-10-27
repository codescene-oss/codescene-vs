// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WorkspaceWatchCoordinatorNestedRepoTests
    {
        [TestMethod]
        public async Task Sync_FolderAboveRepository_WatchesTheNestedRepoOnly()
        {
            var parent = Path.Combine(Path.GetTempPath(), "codescene-watch-" + Guid.NewGuid().ToString("N"));
            var repo = Path.Combine(parent, "nested");
            Directory.CreateDirectory(repo);
            LibGit2Sharp.Repository.Init(repo);
            var modules = Path.Combine(parent, "node_modules");
            Directory.CreateDirectory(modules);
            LibGit2Sharp.Repository.Init(modules);
            Directory.CreateDirectory(Path.Combine(parent, "docs"));
            try
            {
                var client = new Mock<IIdeServerClient>();
                var pipeline = new Mock<IReviewPipeline>();
                var host = new Mock<IWorkspaceWatchHost>();
                host.Setup(x => x.GetDirtyDocuments()).Returns(Array.Empty<ReviewDocument>());
                using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, null, host.Object))
                {
                    await coordinator.SyncAsync(new[] { parent });
                }

                client.Verify(
                    x => x.WatchFiles(
                        It.Is<string>(root => RpcPath.PathsEqual(root, repo)),
                        It.Is<IReadOnlyList<string>>(paths => paths == null)),
                    Times.Once);
                client.Verify(
                    x => x.WatchFiles(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }
    }
}
