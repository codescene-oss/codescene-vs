// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerScanTests : GitChangeDetectorTestBase
    {
        [TestCleanup]
        public void CleanupActivity()
        {
            WorkspaceActivityTracker.Reset();
        }

        [TestMethod]
        public async Task PeriodicScan_SecondIdleTick_SkipsFilesDetected()
        {
            var testableLister = CreateTestableLister();
            var modifiedFile = Path.Combine(_testRepoPath, "idle-skip.cs");
            File.WriteAllText(modifiedFile, "new content");

            var detectionCount = 0;
            testableLister.FilesDetected += (_, __) => detectionCount++;

            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(1, detectionCount, "First scan should detect files");

            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(1, detectionCount, "Idle second scan should not fire FilesDetected");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_UnchangedFileSetWithActivity_SkipsFilesDetected()
        {
            var testableLister = CreateTestableLister();
            var modifiedFile = Path.Combine(_testRepoPath, "unchanged-set.cs");
            File.WriteAllText(modifiedFile, "new content");

            var detectionCount = 0;
            testableLister.FilesDetected += (_, __) => detectionCount++;

            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(1, detectionCount);

            WorkspaceActivityTracker.MarkActivity();
            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(1, detectionCount, "Unchanged file set should not re-fire FilesDetected");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_MarkDirty_ForcesRescanWhenIdle()
        {
            var testableLister = CreateTestableLister();
            var modifiedFile = Path.Combine(_testRepoPath, "mark-dirty.cs");
            File.WriteAllText(modifiedFile, "new content");

            var detectionCount = 0;
            testableLister.FilesDetected += (_, __) => detectionCount++;

            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(1, detectionCount);

            testableLister.MarkDirty();
            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(2, detectionCount, "MarkDirty should force another FilesDetected even when idle");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WorkspaceActivity_ForcesGitScanWhenIdle()
        {
            var testableLister = CreateTestableLister();
            var modifiedFile = Path.Combine(_testRepoPath, "activity.cs");
            File.WriteAllText(modifiedFile, "new content");

            var detectionCount = 0;
            testableLister.FilesDetected += (_, __) => detectionCount++;

            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(1, detectionCount);

            WorkspaceActivityTracker.MarkActivity();
            testableLister.MarkDirty();
            await testableLister.InvokePeriodicScanAsync();
            Assert.AreEqual(2, detectionCount, "Activity plus dirty should allow another detection pass");

            testableLister.Dispose();
        }

        private TestableGitChangeLister CreateTestableLister()
        {
            var lister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);
            lister.Initialize(_testRepoPath, new[] { _testRepoPath });
            return lister;
        }
    }
}
