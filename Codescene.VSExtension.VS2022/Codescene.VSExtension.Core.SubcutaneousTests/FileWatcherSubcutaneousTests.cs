// Copyright (c) CodeScene. All rights reserved.

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
