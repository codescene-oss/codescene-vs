// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;
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

        [TestMethod]
        public async Task PeriodicScan_WhenBothRepoStatesNull_UsesStatusOnly()
        {
            var nonGitPath = Path.Combine(Path.GetTempPath(), $"non-git-{Guid.NewGuid()}");
            Directory.CreateDirectory(nonGitPath);

            try
            {
                var testableLister = new TestableGitChangeLister(
                    _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
                testableLister.Initialize(nonGitPath, nonGitPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();
                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(1, testableLister.CollectFilesFromRepoStateCallCount);
                Assert.AreEqual(0, testableLister.GetAllChangedFilesCallCount);
                testableLister.Dispose();
            }
            finally
            {
                try
                {
                    Directory.Delete(nonGitPath, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public async Task PeriodicScan_WhenRepoBecomesInvalidAfterFirstScan_UsesFullScan()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(_testRepoPath, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.ResetCallCounts();

                var gitDir = Path.Combine(_testRepoPath, ".git");
                foreach (var file in Directory.GetFiles(gitDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                    }
                }

                Directory.Move(gitDir, gitDir + ".bak");

                await testableLister.InvokePeriodicScanAsync();

                Assert.AreEqual(1, testableLister.GetAllChangedFilesCallCount);
                testableLister.Dispose();
            }
            finally
            {
                var gitDir = Path.Combine(_testRepoPath, ".git");
                if (Directory.Exists(gitDir + ".bak"))
                {
                    try
                    {
                        Directory.Move(gitDir + ".bak", gitDir);
                    }
                    catch
                    {
                    }
                }
            }
        }

        [TestMethod]
        public async Task PeriodicScan_WithNullGitRoot_DoesNotThrow()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(null, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                await testableLister.InvokePeriodicScanAsync();
                testableLister.Dispose();
            }
            catch
            {
                Assert.Fail("Should not throw");
            }
        }

        [TestMethod]
        public async Task PeriodicScan_WithEmptyGitRoot_DoesNotThrow()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            try
            {
                testableLister.Initialize(string.Empty, _testRepoPath);
                await testableLister.InvokePeriodicScanAsync();
                await testableLister.InvokePeriodicScanAsync();
                testableLister.Dispose();
            }
            catch
            {
                Assert.Fail("Should not throw");
            }
        }

        [TestMethod]
        public async Task PeriodicScan_WithUnbornRepo_DoesNotThrow()
        {
            var unbornPath = Path.Combine(Path.GetTempPath(), $"unborn-{Guid.NewGuid()}");
            Directory.CreateDirectory(unbornPath);
            Repository.Init(unbornPath);

            using (var repo = new Repository(unbornPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test");
            }

            try
            {
                var testableLister = new TestableGitChangeLister(
                    _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
                testableLister.Initialize(unbornPath, unbornPath);
                await testableLister.InvokePeriodicScanAsync();
                testableLister.Dispose();
            }
            finally
            {
                try
                {
                    Directory.Delete(unbornPath, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public async Task PeriodicScan_WhenGetRepoStateThrows_LogsDebugAndContinues()
        {
            var badRepoPath = Path.Combine(Path.GetTempPath(), $"bad-repo-{Guid.NewGuid()}");
            Directory.CreateDirectory(badRepoPath);
            File.WriteAllText(Path.Combine(badRepoPath, ".git"), "not a directory");

            try
            {
                var testableLister = new TestableGitChangeLister(
                    _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
                testableLister.Initialize(badRepoPath, badRepoPath);
                _fakeLogger.DebugMessages.Clear();
                await testableLister.InvokePeriodicScanAsync();

                Assert.IsTrue(
                    _fakeLogger.DebugMessages.Exists(m => m.Contains("Error getting repo state")),
                    "Should log debug when GetRepoStateAsync fails");
                testableLister.Dispose();
            }
            finally
            {
                try
                {
                    Directory.Delete(badRepoPath, true);
                }
                catch
                {
                }
            }
        }
    }
}
