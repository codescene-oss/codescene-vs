// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerCoverageTests : GitChangeDetectorTestBase
    {
        private Application.Git.GitChangeLister _lister;

        [TestInitialize]
        public void SetupLister()
        {
            _lister = new Application.Git.GitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
        }

        [TestCleanup]
        public void CleanupLister()
        {
            _lister?.Dispose();
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_NullGitRootPath_ReturnsEmptySet()
        {
            var result = await _lister.GetAllChangedFilesAsync(null, _testRepoPath);
            Assert.IsEmpty(result, "Null git root path should return empty set");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_EmptyGitRootPath_ReturnsEmptySet()
        {
            var result = await _lister.GetAllChangedFilesAsync(string.Empty, _testRepoPath);
            Assert.IsEmpty(result, "Empty git root path should return empty set");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_NonExistentPath_ReturnsEmptySet()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var result = await _lister.GetAllChangedFilesAsync(nonExistentPath, _testRepoPath);
            Assert.IsEmpty(result, "Non-existent path should return empty set");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_NonGitDirectory_ReturnsEmptySet()
        {
            var nonGitDir = Path.Combine(Path.GetTempPath(), $"non-git-{Guid.NewGuid()}");
            Directory.CreateDirectory(nonGitDir);
            try
            {
                var result = await _lister.GetAllChangedFilesAsync(nonGitDir, nonGitDir);
                Assert.IsEmpty(result, "Non-git directory should return empty set");
            }
            finally
            {
                Directory.Delete(nonGitDir, true);
            }
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_CorruptedGitRepo_ReturnsEmptySetAndLogsError()
        {
            CorruptGitIndex();
            _fakeLogger.WarnMessages.Clear();
            _fakeLogger.DebugMessages.Clear();

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.IsEmpty(result, "Corrupted git repo should return empty set");
            Assert.IsTrue(
                _fakeLogger.WarnMessages.Exists(m => m.Contains("Error")) ||
                _fakeLogger.DebugMessages.Exists(m => m.Contains("Error")),
                "Should log error about corrupted repo");
        }

        [TestMethod]
        public async Task CollectFilesFromRepoStateAsync_WithModifiedFiles_ReturnsExpectedFiles()
        {
            var modifiedFile = Path.Combine(_testRepoPath, "modified.cs");
            CommitFile("modified.cs", "original content", "Add file");
            File.WriteAllText(modifiedFile, "modified content");

            var result = await _lister.CollectFilesFromRepoStateAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should return one modified file");
            Assert.Contains(modifiedFile, result, "Should contain the modified file");
        }

        [TestMethod]
        public async Task PeriodicScan_WithModifiedFiles_FiresFilesDetectedEvent()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            testableLister.Initialize(_testRepoPath, _testRepoPath);

            var modifiedFile = Path.Combine(_testRepoPath, "periodic.cs");
            File.WriteAllText(modifiedFile, "new content");

            HashSet<string> detectedFiles = null;
            testableLister.FilesDetected += (sender, files) => detectedFiles = files;

            await testableLister.InvokePeriodicScanAsync();

            Assert.IsNotNull(detectedFiles, "FilesDetected event should have fired");
            Assert.Contains(modifiedFile, detectedFiles, "Should contain the modified file");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WithNoFiles_DoesNotFireEvent()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            testableLister.Initialize(_testRepoPath, _testRepoPath);

            bool eventFired = false;
            testableLister.FilesDetected += (sender, files) => eventFired = true;

            await testableLister.InvokePeriodicScanAsync();

            Assert.IsFalse(eventFired, "FilesDetected event should not fire when no files");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_ExceptionThrown_LogsWarning()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            testableLister.Initialize(_testRepoPath, _testRepoPath);
            testableLister.ThrowInCollectFilesFromRepoStateAsync = true;

            _fakeLogger.WarnMessages.Clear();

            await testableLister.InvokePeriodicScanAsync();

            Assert.IsTrue(
                _fakeLogger.WarnMessages.Exists(m => m.Contains("Error during periodic scan")),
                "Should log warning about periodic scan error");

            testableLister.Dispose();
        }
    }
}
