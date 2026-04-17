// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.SubcutaneousTests;

[TestClass]
public class PollingRaceSubcutaneousTests : SubcutaneousGitTestBase
{
    protected override bool AutoStartObserver => false;

    [TestMethod]
    public async Task PeriodicPolling_DoesNotRunParallelReviewForSameFile()
    {
        const string relativePath = "src/RepeatedPolling.cs";
        var absolutePath = AbsolutePath(relativePath);

        await CreateCommittedFileAsync(relativePath, "public class RepeatedPolling { public int Value => 1; }", "Add repeated polling file");
        CheckoutBranch("feature", create: true);
        await WriteWorkingFileAsync(relativePath, "public class RepeatedPolling { public int Value => 2; }");
        CommitAll("Change repeated polling file");

        var block = CodeReviewer.BlockNextReview(absolutePath);
        try
        {
            await StartObserverAsync();
            await WaitForConditionAsync(() => block.Entered.IsCompleted, "The periodic scan never reached the blocked review.");
            await Task.Delay(1500);
            SnapshotState("while-review-blocked", relativePath);

            Assert.AreEqual(1, ReviewCount(relativePath), Journal.Dump());
            Assert.AreEqual(1, MaxParallelReviews(relativePath), Journal.Dump());

            block.Release();
            await WaitForReviewCountAsync(relativePath, 1);
        }
        finally
        {
            block.Release();
        }
    }

    [TestMethod]
    public async Task StartupRace_ChangeBeforeStart_IsObservedAfterStartup()
    {
        const string relativePath = "src/StartupObserved.cs";

        await WriteWorkingFileAsync(relativePath, "public class StartupObserved { public int Value => 1; }");

        await StartObserverAsync();
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);
        SnapshotState("post-startup-race", relativePath);
    }
}
