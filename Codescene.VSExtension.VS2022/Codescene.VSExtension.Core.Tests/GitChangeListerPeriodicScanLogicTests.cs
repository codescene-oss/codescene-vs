// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerPeriodicScanLogicTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public async Task PeriodicScan_FirstRun_UsesFullScan()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(1, testableLister.GetAllChangedFilesCallCount, "First scan should use GetAllChangedFilesAsync");
                Assert.AreEqual(0, testableLister.CollectFilesFromRepoStateCallCount, "First scan should not use CollectFilesFromRepoStateAsync");
            }
            finally
            {
                testableLister.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScan_SecondRunWithNoRepoChanges_UsesStatusOnly()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();
                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(0, testableLister.GetAllChangedFilesCallCount, "Second scan with no repo changes should not use full scan");
                Assert.AreEqual(1, testableLister.CollectFilesFromRepoStateCallCount, "Second scan should use CollectFilesFromRepoStateAsync");
            }
            finally
            {
                testableLister.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScan_AfterNewCommit_UsesFullScan()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();

                CommitFile("new-commit.cs", "content", "New commit");

                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(1, testableLister.GetAllChangedFilesCallCount, "Scan after new commit should use full scan");
                Assert.AreEqual(0, testableLister.CollectFilesFromRepoStateCallCount, "Should not use status-only after HEAD change");
            }
            finally
            {
                testableLister.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScan_AfterBranchSwitch_UsesFullScan()
        {
            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "content", "Add feature");

            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();

                ExecGit("checkout master");

                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(1, testableLister.GetAllChangedFilesCallCount, "Scan after branch switch should use full scan");
                Assert.AreEqual(0, testableLister.CollectFilesFromRepoStateCallCount, "Should not use status-only after branch change");
            }
            finally
            {
                testableLister.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScan_AfterReinitialize_ResetsToFullScan()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();

                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(1, testableLister.GetAllChangedFilesCallCount, "Scan after reinitialize should use full scan");
                Assert.AreEqual(0, testableLister.CollectFilesFromRepoStateCallCount, "Should not use status-only after reinitialize");
            }
            finally
            {
                testableLister.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScan_AfterMergeBaseChange_UsesFullScan()
        {
            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "content", "Add feature");

            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();

                ExecGit("checkout master");
                CommitFile("main-update.cs", "content", "Update main");
                ExecGit("checkout feature-branch");
                ExecGit("merge master");

                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(1, testableLister.GetAllChangedFilesCallCount, "Scan after merge (merge base change) should use full scan");
            }
            finally
            {
                testableLister.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScan_LocalModificationOnly_DoesNotTriggerFullScan()
        {
            CommitFile("tracked.cs", "original", "Add tracked");
            var modifiedFile = Path.Combine(_testRepoPath, "tracked.cs");
            File.WriteAllText(modifiedFile, "modified");

            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();

                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(0, testableLister.GetAllChangedFilesCallCount, "Local modification without commit should not trigger full scan");
                Assert.AreEqual(1, testableLister.CollectFilesFromRepoStateCallCount, "Should use status-only for uncommitted changes");
            }
            finally
            {
                testableLister.Dispose();
            }
        }
    }
}
