// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

[TestClass]
public class WatchWorkflowSubcutaneousTests : SubcutaneousGitTestBase
{
    [TestMethod]
    public async Task WatchInventory_PrunesStaleMonitorEntriesAndKeepsDirtyBuffers()
    {
        var dirtyPath = AbsolutePath("src/dirty.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(dirtyPath));
        File.WriteAllText(dirtyPath, "class Dirty {}");
        File.WriteAllText(AbsolutePath("src/stale.cs"), "class Stale {}");

        var client = new Mock<IIdeServerClient>();
        var pipeline = new Mock<IReviewPipeline>();
        ISet<string> keep = null;
        var host = new Mock<IWorkspaceWatchHost>();
        host.Setup(x => x.GetDirtyDocuments()).Returns(new[]
        {
            new ReviewDocument { FilePath = dirtyPath, Content = "class Dirty {}", IsDirty = true },
        });
        host.Setup(x => x.PruneMonitor(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ISet<string>>()))
            .Callback<IReadOnlyList<string>, ISet<string>>((inventory, paths) => keep = paths);

        using (var coordinator = new WorkspaceWatchCoordinator(client.Object, pipeline.Object, Logger, host.Object))
        {
            await coordinator.SyncAsync(new[] { RepositoryRoot });
            client.Raise(
                x => x.WatchInventoryChanged += null,
                client.Object,
                new WatchInventory { RepoRoot = RepositoryRoot, Files = new[] { "src/tracked.cs" } });

            Assert.IsNotNull(keep);
            Assert.IsTrue(keep.Any(path => path.EndsWith("tracked.cs", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(dirtyPath, keep);
            Assert.IsFalse(keep.Any(path => path.EndsWith("stale.cs", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [TestMethod]
    public async Task IdLessWatchReview_PresentsWhenDocumentIsOnDisk()
    {
        var filePath = await WriteWorkingFileAsync("src/watched.cs", "class Watched {}");
        var sha = GitBlobSha.FromBytes(File.ReadAllBytes(filePath));
        var client = new Mock<IIdeServerClient>();
        var events = new List<PresentedReview>();
        var presentation = new Mock<IReviewPipelinePresentation>();
        presentation.Setup(x => x.PresentReview(It.IsAny<PresentedReview>()))
            .Callback<PresentedReview>(events.Add);

        using (var pipeline = new ReviewPipeline(client.Object, presentation.Object, null, Logger))
        {
            client.Raise(
                x => x.ReviewReceived += null,
                client.Object,
                new ReviewNotification
                {
                    RepoRoot = RepositoryRoot,
                    Path = "src/watched.cs",
                    Result = new CliReviewModel
                    {
                        FileLevelCodeSmells = new List<CliCodeSmellModel>(),
                        FunctionLevelCodeSmells = new List<CliReviewFunctionModel>(),
                        RawScore = "raw",
                        GitBlobSha = sha,
                    },
                });

            for (var i = 0; i < 40 && events.Count == 0; i++)
            {
                await Task.Delay(10);
            }
        }

        Assert.HasCount(1, events);
        Assert.AreEqual(filePath, events[0].Document.FilePath);
        Assert.IsTrue(events[0].UpdateMonitor);
        Assert.IsFalse(events[0].UpdateDiagnosticsPane);
    }

    [TestMethod]
    public async Task IdLessWatchDelta_PresentsDiskDocument()
    {
        var filePath = await WriteWorkingFileAsync("src/watched.cs", "class Watched {}");
        var sha = GitBlobSha.FromBytes(File.ReadAllBytes(filePath));
        var client = new Mock<IIdeServerClient>();
        var events = new List<PresentedDelta>();
        var presentation = new Mock<IReviewPipelinePresentation>();
        presentation.Setup(x => x.PresentDelta(It.IsAny<PresentedDelta>()))
            .Callback<PresentedDelta>(events.Add);

        using (var pipeline = new ReviewPipeline(client.Object, presentation.Object, null, Logger))
        {
            client.Raise(
                x => x.DeltaReceived += null,
                client.Object,
                new DeltaNotification
                {
                    RepoRoot = RepositoryRoot,
                    Path = "src/watched.cs",
                    Result = new DeltaResponseModel
                    {
                        NewGitBlobSha = sha,
                        ScoreChange = 0.1m,
                    },
                });

            for (var i = 0; i < 40 && events.Count == 0; i++)
            {
                await Task.Delay(10);
            }
        }

        Assert.HasCount(1, events);
        Assert.IsTrue(events[0].UpdateMonitor);
    }
}
