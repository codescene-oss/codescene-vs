// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Util;
using LibGit2Sharp;
using WebComponentFile = Codescene.VSExtension.Core.Models.WebComponent.Data.File;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerEdgeCasesTests : GitChangeDetectorTestBase
    {
        private GitChangeLister _lister;

        [TestInitialize]
        public void SetupLister()
        {
            _lister = new GitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);
        }

        [TestCleanup]
        public void CleanupLister()
        {
            _lister?.Dispose();
            DeltaJobTracker.Clear();
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_WithNullPath_ReturnsEmptySet()
        {
            var result = await _lister.GetAllChangedFilesAsync(null, _testRepoPath);

            Assert.IsEmpty(result, "Should return empty set for null path");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_WithEmptyPath_ReturnsEmptySet()
        {
            var result = await _lister.GetAllChangedFilesAsync(string.Empty, _testRepoPath);

            Assert.IsEmpty(result, "Should return empty set for empty path");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_WithNonExistentPath_ReturnsEmptySet()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid()}");

            var result = await _lister.GetAllChangedFilesAsync(nonExistentPath, _testRepoPath);

            Assert.IsEmpty(result, "Should return empty set for non-existent path");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_WithNonGitDirectory_ReturnsEmptySet()
        {
            var nonGitPath = Path.Combine(Path.GetTempPath(), $"non-git-{Guid.NewGuid()}");
            Directory.CreateDirectory(nonGitPath);

            try
            {
                var result = await _lister.GetAllChangedFilesAsync(nonGitPath, nonGitPath);

                Assert.IsEmpty(result, "Should return empty set for non-git directory");
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
        public async Task GetAllChangedFilesAsync_WithCorruptedGitIndex_HandlesGracefully()
        {
            CorruptGitIndex();

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.IsNotNull(result, "Should return a result even with corrupted git index");
        }

        [TestMethod]
        public async Task CollectFilesFromRepoStateAsync_DetectsModifiedFiles()
        {
            var modifiedFile = Path.Combine(_testRepoPath, "modified-state.cs");
            CommitFile("modified-state.cs", "original", "Add file");
            File.WriteAllText(modifiedFile, "modified");

            var result = await _lister.CollectFilesFromRepoStateAsync(_testRepoPath, new[] { _testRepoPath });

            Assert.HasCount(1, result, "Should detect one modified file");
            Assert.Contains(modifiedFile, result, "Should contain the modified file");
        }

        [TestMethod]
        public async Task PeriodicScanAsync_DetectsChangesAndFiresEvent()
        {
            var testableInstance = new TestableGitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);

            try
            {
                testableInstance.Initialize(_testRepoPath, new[] { _testRepoPath });

                var newFile = Path.Combine(_testRepoPath, "periodic.cs");
                File.WriteAllText(newFile, "content");

                HashSet<string> detectedFiles = null;
                testableInstance.FilesDetected += (sender, files) => detectedFiles = files;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.IsNotNull(detectedFiles, "Should fire event when files detected");
                Assert.Contains(newFile, detectedFiles, "Should detect the new file");
            }
            finally
            {
                testableInstance?.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScanAsync_WithNoChanges_DoesNotFireEvent()
        {
            var testableInstance = new TestableGitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);

            try
            {
                testableInstance.Initialize(_testRepoPath, new[] { _testRepoPath });

                var eventFired = false;
                testableInstance.FilesDetected += (sender, files) => eventFired = true;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.IsFalse(eventFired, "Should not fire event when no files detected");
            }
            finally
            {
                testableInstance?.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScanAsync_WhenExceptionThrown_LogsError()
        {
            var testableInstance = new TestableGitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);

            try
            {
                testableInstance.Initialize(_testRepoPath, new[] { _testRepoPath });
                testableInstance.ThrowInGetAllChangedFilesAsync = true;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.IsTrue(_fakeLogger.SnapshotErrorMessages().Any(m => m.Item1.Contains("Error during periodic scan")), "Should log error on exception");
            }
            finally
            {
                testableInstance?.Dispose();
            }
        }

        [TestMethod]
        public async Task GetChangedFilesVsMergeBaseAsync_WithCorruptedRepo_HandlesGracefully()
        {
            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "feature content", "Add feature");

            CorruptGitObjects();

            var result = await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);

            Assert.IsNotNull(result, "Should return a result even with corrupted repo");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_OnFeatureBranchWithCorruptedMainBranch_HandlesGracefully()
        {
            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "feature content", "Add feature");

            CorruptGitObjects();

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.IsNotNull(result, "Should handle corrupted main branch gracefully");
        }

        [TestMethod]
        public async Task PeriodicScanAsync_SkipsWhenIdeWindowNotFocused()
        {
            var activityTracker = new FakeIdeActivityTracker();
            activityTracker.SetActiveForTesting(false);

            var testableInstance = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                activityTracker);

            try
            {
                testableInstance.Initialize(_testRepoPath, new[] { _testRepoPath });

                var newFile = Path.Combine(_testRepoPath, "should-not-scan.cs");
                File.WriteAllText(newFile, "content");

                var eventFired = false;
                testableInstance.FilesDetected += (sender, files) => eventFired = true;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.IsFalse(eventFired, "Should not fire event when IDE window not focused");
            }
            finally
            {
                testableInstance?.Dispose();
            }
        }

        [TestMethod]
        public async Task PeriodicScanAsync_ProceedsWhenIdeWindowFocused()
        {
            var activityTracker = new FakeIdeActivityTracker();
            activityTracker.SetActiveForTesting(true);

            var testableInstance = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                activityTracker);

            try
            {
                testableInstance.Initialize(_testRepoPath, new[] { _testRepoPath });

                var newFile = Path.Combine(_testRepoPath, "should-scan.cs");
                File.WriteAllText(newFile, "content");

                HashSet<string> detectedFiles = null;
                testableInstance.FilesDetected += (sender, files) => detectedFiles = files;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.IsNotNull(detectedFiles, "Should fire event when IDE window is focused");
                Assert.Contains(newFile, detectedFiles, "Should detect the new file");
            }
            finally
            {
                testableInstance?.Dispose();
            }
        }

        [TestMethod]
        [DataRow(0, true, DisplayName = "proceeds when no analysis is running")]
        [DataRow(1, false, DisplayName = "skips when analysis is running")]
        public async Task PeriodicScanAsync_WhenAnalysisRunning_SkipsOrProceeds(int runningJobCount, bool expectEvent)
        {
            var jobs = new List<Job>();
            for (int i = 0; i < runningJobCount; i++)
            {
                var job = new Job { Type = "deltaAnalysis", State = "running", File = new WebComponentFile { FileName = $"test{i}.cs" } };
                DeltaJobTracker.Add(job);
                jobs.Add(job);
            }

            var activityTracker = new FakeIdeActivityTracker();
            activityTracker.SetActiveForTesting(true);

            var testableInstance = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                activityTracker);

            try
            {
                testableInstance.Initialize(_testRepoPath, new[] { _testRepoPath });

                var newFile = Path.Combine(_testRepoPath, "analysis-check.cs");
                System.IO.File.WriteAllText(newFile, "content");

                var eventFired = false;
                testableInstance.FilesDetected += (sender, files) => eventFired = true;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.AreEqual(expectEvent, eventFired);
            }
            finally
            {
                testableInstance?.Dispose();
                foreach (var job in jobs)
                {
                    DeltaJobTracker.Remove(job);
                }
            }
        }

        [TestMethod]
        public async Task PeriodicScanAsync_SkipsWhenOnDefaultBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var currentBranch = repo.Head.FriendlyName;
                repo.Refs.Add($"refs/remotes/origin/{currentBranch}", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs[$"refs/remotes/origin/{currentBranch}"]);
            }

            var defaultBranchGate = new DefaultBranchGate(_testRepoPath);

            var testableInstance = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService);

            try
            {
                testableInstance.Initialize(_testRepoPath, new[] { _testRepoPath }, defaultBranchGate);

                var newFile = Path.Combine(_testRepoPath, "should-not-scan-on-main.cs");
                File.WriteAllText(newFile, "content");

                var eventFired = false;
                testableInstance.FilesDetected += (sender, files) => eventFired = true;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.IsFalse(eventFired, "Should not fire event when on default branch");
            }
            finally
            {
                testableInstance?.Dispose();
            }
        }
    }
}
