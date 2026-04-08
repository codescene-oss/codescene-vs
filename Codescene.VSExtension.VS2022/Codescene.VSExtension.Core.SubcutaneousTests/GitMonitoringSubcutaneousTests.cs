// Copyright (c) CodeScene. All rights reserved.

using System.Linq;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

[TestClass]
public class FileWatcherSubcutaneousTests : SubcutaneousGitTestBase
{
    protected override int GitPollingIntervalSeconds => 30;

    [TestMethod]
    public async Task FileCreate_BecomesTrackedAndReviewedOnce()
    {
        const string relativePath = "src/CreateTracked.cs";

        await WriteWorkingFileAsync(relativePath, "public class CreateTracked { public int Value => 1; }");

        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);
        await Task.Delay(1500);
        SnapshotState("post-create", relativePath);

        Assert.AreEqual(1, ReviewCount(relativePath), Journal.Dump());
    }

    [TestMethod]
    public async Task RapidRepeatedWrites_BeforeFirstTick_ResultInSingleReview()
    {
        const string relativePath = "src/RapidWrites.cs";

        await WriteWorkingFileAsync(relativePath, "public class RapidWrites { public int Value => 1; }");
        await AppendWorkingFileAsync(relativePath, Environment.NewLine + "public int Value2 => 2;");
        await AppendWorkingFileAsync(relativePath, Environment.NewLine + "public int Value3 => 3;");

        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);
        await Task.Delay(1500);
        SnapshotState("post-rapid-writes", relativePath);

        Assert.AreEqual(1, ReviewCount(relativePath), Journal.Dump());
    }

    [TestMethod]
    public async Task FileModify_AfterInitialReview_ProducesSingleAdditionalReview()
    {
        const string relativePath = "src/SingleAdditionalReview.cs";

        await WriteWorkingFileAsync(relativePath, "public class SingleAdditionalReview { public int Value => 1; }");
        await WaitForReviewCountAsync(relativePath, 1);

        await WriteWorkingFileAsync(relativePath, "public class SingleAdditionalReview { public int Value => 2; }");
        await WriteWorkingFileAsync(relativePath, "public class SingleAdditionalReview { public int Value => 3; }");

        await WaitForReviewCountAsync(relativePath, 2);
        await Task.Delay(1500);
        SnapshotState("post-modify", relativePath);

        Assert.AreEqual(2, ReviewCount(relativePath), Journal.Dump());
    }

    [TestMethod]
    public async Task FileCreatedThenDeletedWhileReviewBlocked_DoesNotLeaveStaleDelta()
    {
        const string relativePath = "src/DeleteDuringReview.cs";
        var absolutePath = AbsolutePath(relativePath);
        var block = CodeReviewer.BlockNextReview(absolutePath);
        try
        {
            await WriteWorkingFileAsync(relativePath, "public class DeleteDuringReview { public int Value => 1; }");
            await WaitForConditionAsync(() => block.Entered.IsCompleted, "The blocked review never started.");

            DeleteWorkingFile(relativePath);

            block.Release();
            await TaskScheduler.WaitForIdleAsync(10000);
            await WaitForNotTrackedAsync(relativePath);
            await WaitForNoDeltaAsync(relativePath);
            SnapshotState("post-delete-during-review", relativePath);

            Assert.IsGreaterThanOrEqualTo(Journal.Count("observer.file-deleted", absolutePath), 1, Journal.Dump());
        }
        finally
        {
            block.Release();
        }
    }
}

[TestClass]
public class GitCommandSubcutaneousTests : SubcutaneousGitTestBase
{
    protected override int GitPollingIntervalSeconds => 5;

    protected override bool AutoStartObserver => false;

    [TestMethod]
    public async Task GitCheckout_SwitchesCommittedChangeInAndOutOfMonitor()
    {
        const string relativePath = "src/BranchSwitch.cs";

        await CreateCommittedFileAsync(relativePath, "public class BranchSwitch { public int Value => 1; }", "Add branch switch file");
        CheckoutBranch("feature", create: true);
        await WriteWorkingFileAsync(relativePath, "public class BranchSwitch { public int Value => 2; }");
        CommitAll("Change branch switch file on feature");
        CheckoutBranch("main");

        await StartObserverAsync();

        CheckoutBranch("feature");
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);

        CheckoutBranch("main");
        await WaitForNotTrackedAsync(relativePath);
        await WaitForNoDeltaAsync(relativePath);
        SnapshotState("post-checkout-main", relativePath);
    }

    [TestMethod]
    public async Task BranchSwitchReset_ClearsRunningDeltaJobForBlockedReview()
    {
        const string relativePath = "src/InfiniteSpinner.cs";
        var absolutePath = AbsolutePath(relativePath);
        const string changedContent = @"public class InfiniteSpinner
{
    public int Compute(int value, int factor, bool includeBonus)
    {
        var total = value + factor;

        if (includeBonus)
        {
            total += 5;
        }

        if (total > 10)
        {
            if (total % 2 == 0)
            {
                total += factor;
            }
            else
            {
                total -= value;
            }
        }
        else if (total > 5)
        {
            total = total * 2;
        }
        else
        {
            total = total - 3;
        }

        return total;
    }
}";

        await CreateCommittedFileAsync(relativePath, "public class InfiniteSpinner { public int Value => 1; }", "Add spinner file");
        CheckoutBranch("feature", create: true);

        var block = CliExecutor.BlockNextDelta(absolutePath);
        try
        {
            await StartObserverAsync();
            await WriteWorkingFileAsync(relativePath, changedContent);
            await WaitForTrackedAsync(relativePath);
            await WaitForConditionAsync(() => block.Entered.IsCompleted, "The blocked delta review never started.");
            await WaitForConditionAsync(() => HasRunningDeltaJob(relativePath), "Expected a running delta job before the branch reset.");

            CheckoutBranch("main");
            Observer.CancelAndReset();

            await WaitForConditionAsync(
                () => RunningDeltaJobCount() == 0,
                $"Expected branch reset to clear all running delta jobs, but found {RunningDeltaJobCount()}.");
            SnapshotState("post-branch-switch-reset", relativePath);

            block.Release();
            await TaskScheduler.WaitForIdleAsync(10000);

            Assert.AreEqual(0, RunningDeltaJobCount(), Journal.Dump());
        }
        finally
        {
            block.Release();
        }
    }

    [TestMethod]
    public async Task BranchSwitch_StaleDeltaFromPreviousBranch_DoesNotLeakIntoCache()
    {
        const string branchAPath = "src/BranchAOnly.cs";
        const string branchBPath = "src/BranchBOnly.cs";
        const string baseContent = "public class SharedBase { public int Value => 1; }";
        const string branchAContent = @"public class SharedBase
{
    public int ComputeAlpha(int first, int second, bool includeThirdStep)
    {
        var total = first + second;

        if (includeThirdStep)
        {
            total += 7;
        }

        if (total > 20)
        {
            total = total - first;
        }
        else if (total > 10)
        {
            total = total * 2;
        }
        else
        {
            total = total + second;
        }

        return total;
    }
}";
        const string branchBContent = @"public class SharedBase
{
    public int ComputeBeta(int value)
    {
        var current = value;

        for (var index = 0; index < 3; index++)
        {
            current += index;
        }

        if (current % 2 == 0)
        {
            current = current / 2;
        }
        else
        {
            current = current + 9;
        }

        return current;
    }
}";

        await CreateCommittedFileAsync(branchAPath, baseContent, "Add branch a base file");
        await CreateCommittedFileAsync(branchBPath, baseContent, "Add branch b base file");

        CheckoutBranch("branch-a", create: true);
        await WriteWorkingFileAsync(branchAPath, branchAContent);
        CommitAll("Change file on branch a");

        CheckoutBranch("main");
        CheckoutBranch("branch-b", create: true);
        await WriteWorkingFileAsync(branchBPath, branchBContent);
        CommitAll("Change file on branch b");

        CheckoutBranch("main");

        var absoluteBranchAPath = AbsolutePath(branchAPath);
        var absoluteBranchBPath = AbsolutePath(branchBPath);
        var block = CliExecutor.BlockNextDelta(absoluteBranchAPath);
        try
        {
            await StartObserverAsync();

            CheckoutBranch("branch-a");
            await WaitForConditionAsync(() => block.Entered.IsCompleted, "The branch a delta review never started.");
            await WaitForConditionAsync(() => HasRunningDeltaJob(branchAPath), "Expected a running delta job for branch a.");

            CheckoutBranch("branch-b");
            Observer.CancelAndReset();

            await WaitForTrackedAsync(branchBPath);
            await WaitForReviewCountAsync(branchBPath, 1);
            SnapshotState("branch-b-visible", branchAPath, branchBPath);

            block.Release();
            await TaskScheduler.WaitForIdleAsync(10000);
            await WaitForConditionAsync(
                () => !HasDelta(branchAPath),
                "Expected stale branch a delta to stay out of the cache after switching to branch b.");
            SnapshotState("post-branch-switch-cache-race", branchAPath, branchBPath);

            Assert.IsFalse(
                DeltaCachePaths().Any(p => string.Equals(p, absoluteBranchAPath, StringComparison.OrdinalIgnoreCase)),
                Journal.Dump());
            Assert.IsTrue(
                DeltaCachePaths().All(p => string.Equals(p, absoluteBranchBPath, StringComparison.OrdinalIgnoreCase)),
                Journal.Dump());
        }
        finally
        {
            block.Release();
        }
    }

    [TestMethod]
    public async Task GitCheckoutFile_RevertsWorkingTreeChangeAndClearsTrackedState()
    {
        const string relativePath = "src/CheckoutRestore.cs";

        await CreateCommittedFileAsync(relativePath, "public class CheckoutRestore { public int Value => 1; }", "Add checkout restore file");

        await StartObserverAsync();
        await WriteWorkingFileAsync(relativePath, "public class CheckoutRestore { public int Value => 2; }");
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);

        ExecGit("checkout -- src/CheckoutRestore.cs");

        await WaitForNotTrackedAsync(relativePath);
        await WaitForNoDeltaAsync(relativePath);
        SnapshotState("post-checkout-file-restore", relativePath);
    }

    [TestMethod]
    public async Task GitMove_TracksNewPathAndClearsOldPath()
    {
        const string oldRelativePath = "src/OldName.cs";
        const string newRelativePath = "src/NewName.cs";

        await StartObserverAsync();
        await WriteWorkingFileAsync(oldRelativePath, "public class OldName { public int Value => 1; }");
        await WaitForTrackedAsync(oldRelativePath);
        await WaitForReviewCountAsync(oldRelativePath, 1);

        ExecGit("add src/OldName.cs");
        ExecGit("mv src/OldName.cs src/NewName.cs");

        await WaitForTrackedAsync(newRelativePath);
        await WaitForReviewCountAsync(newRelativePath, 1);
        await WaitForNotTrackedAsync(oldRelativePath);
        await WaitForNoDeltaAsync(oldRelativePath);
        SnapshotState("post-git-mv", oldRelativePath, newRelativePath);
    }

    [TestMethod]
    public async Task GitResetHard_RemovesFeatureOnlyCommittedFile()
    {
        const string relativePath = "src/ResetTracked.cs";

        CheckoutBranch("feature", create: true);
        await CreateCommittedFileAsync(relativePath, "public class ResetTracked { public int Value => 1; }", "Add feature-only file");

        await StartObserverAsync();
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);

        ResetHard("HEAD~1");

        await WaitForNotTrackedAsync(relativePath);
        await WaitForNoDeltaAsync(relativePath);
        SnapshotState("post-reset-hard", relativePath);
    }

    [TestMethod]
    public async Task GitStashPushAndPop_TogglesTrackedStateForUntrackedFile()
    {
        const string relativePath = "src/StashedFile.cs";

        await StartObserverAsync();
        await WriteWorkingFileAsync(relativePath, "public class StashedFile { public int Value => 1; }");
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);

        StashPush("stash untracked file", includeUntracked: true);

        await WaitForNotTrackedAsync(relativePath);
        await WaitForNoDeltaAsync(relativePath);

        StashPop();

        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 2);
        SnapshotState("post-stash-pop", relativePath);
    }

    [TestMethod]
    public async Task GitStashPopConflict_LeavesFileTrackedWithoutParallelReviews()
    {
        const string relativePath = "src/StashConflict.cs";
        var absolutePath = AbsolutePath(relativePath);

        await CreateCommittedFileAsync(relativePath, "public class StashConflict { public int Value => 1; }", "Add stash conflict file");

        await StartObserverAsync();

        await WriteWorkingFileAsync(relativePath, "public class StashConflict { public int Value => 2; }");
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);

        StashPush("stash conflicting change");

        await WriteWorkingFileAsync(relativePath, "public class StashConflict { public int Value => 3; }");
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 2);
        CommitAll("Commit conflicting change");

        var result = StashPopAllowFailure();

        Assert.AreNotEqual(0, result.ExitCode, Journal.Dump());

        await WaitForConditionAsync(
            () => System.IO.File.ReadAllText(absolutePath).Contains("<<<<<<<"),
            "Expected stash pop to leave conflict markers in the file.");
        await WaitForTrackedAsync(relativePath);
        await Task.Delay(1500);
        SnapshotState("post-stash-conflict", relativePath);

        Assert.AreEqual(1, MaxParallelReviews(relativePath), Journal.Dump());
    }

    [TestMethod]
    public async Task GitRebase_KeepsFeatureChangeTrackedWithoutParallelReviews()
    {
        const string relativePath = "src/RebasedFile.cs";

        await CreateCommittedFileAsync(relativePath, "public class RebasedFile { public int Value => 1; }", "Add rebased file on main");
        CheckoutBranch("feature", create: true);
        await WriteWorkingFileAsync(relativePath, "public class RebasedFile { public int Value => 2; }");
        CommitAll("Feature change for rebase");

        CheckoutBranch("main");
        await CreateCommittedFileAsync("src/MainSideChange.cs", "public class MainSideChange { public int Value => 1; }", "Add main side change");
        CheckoutBranch("feature");

        await StartObserverAsync();
        await WaitForTrackedAsync(relativePath);
        await WaitForReviewCountAsync(relativePath, 1);

        var baselineParallel = MaxParallelReviews(relativePath);

        RebaseOnto("main");
        var journalCountAfterRebaseGit = Journal.Snapshot().Count;

        await WaitForTrackedAsync(relativePath);
        await WaitForConditionAsync(
            () => Journal.Snapshot().Count > journalCountAfterRebaseGit || MaxParallelReviews(relativePath) != baselineParallel,
            "Expected observer journal activity after rebase was processed.");
        SnapshotState("post-rebase", relativePath);

        Assert.AreEqual(1, MaxParallelReviews(relativePath), Journal.Dump());
    }
}

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
