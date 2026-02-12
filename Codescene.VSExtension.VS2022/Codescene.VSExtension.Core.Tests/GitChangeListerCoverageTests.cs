// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        [TestMethod]
        public void ConvertAndFilterPaths_EmptyInput_ReturnsEmptySet()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);

            var result = testableLister.InvokeConvertAndFilterPaths(new List<string>(), _testRepoPath);

            Assert.IsEmpty(result, "Empty input should return empty set");

            testableLister.Dispose();
        }

        [TestMethod]
        public void ConvertAndFilterPaths_RelativePathsConverted_ToAbsolutePaths()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            var relativePaths = new List<string> { "file1.cs", "subdir/file2.cs" };

            var result = testableLister.InvokeConvertAndFilterPaths(relativePaths, _testRepoPath);

            Assert.HasCount(2, result, "Should convert all paths");
            Assert.IsTrue(result.All(p => Path.IsPathRooted(p)), "All paths should be absolute");

            testableLister.Dispose();
        }

        [TestMethod]
        public void ConvertAndFilterPaths_FiltersUnsupportedFiles()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            var relativePaths = new List<string> { "file1.cs", "file2.txt", "file3.md" };

            var result = testableLister.InvokeConvertAndFilterPaths(relativePaths, _testRepoPath);

            Assert.HasCount(1, result, "Should only include supported files");
            Assert.IsTrue(result.Any(p => p.EndsWith("file1.cs")), "Should include .cs file");

            testableLister.Dispose();
        }

        [TestMethod]
        public void ConvertAndFilterPaths_IncludesOnlySupportedFiles()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            var relativePaths = new List<string> { "file1.cs", "file2.js", "file3.py" };

            var result = testableLister.InvokeConvertAndFilterPaths(relativePaths, _testRepoPath);

            Assert.HasCount(3, result, "Should include all supported file types");

            testableLister.Dispose();
        }

        [TestMethod]
        public void ConvertAndFilterPaths_MixedSupportedAndUnsupported_FiltersProperly()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            var relativePaths = new List<string>
            {
                "supported1.cs",
                "unsupported1.txt",
                "supported2.js",
                "unsupported2.md",
                "supported3.py",
            };

            var result = testableLister.InvokeConvertAndFilterPaths(relativePaths, _testRepoPath);

            Assert.HasCount(3, result, "Should include only supported files");
            Assert.IsTrue(result.Any(p => p.EndsWith("supported1.cs")), "Should include .cs file");
            Assert.IsTrue(result.Any(p => p.EndsWith("supported2.js")), "Should include .js file");
            Assert.IsTrue(result.Any(p => p.EndsWith("supported3.py")), "Should include .py file");
            Assert.IsFalse(result.Any(p => p.EndsWith(".txt")), "Should not include .txt file");
            Assert.IsFalse(result.Any(p => p.EndsWith(".md")), "Should not include .md file");

            testableLister.Dispose();
        }

        [TestMethod]
        public void ConvertAndFilterPaths_WithSubdirectories_HandlesProperly()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
            var relativePaths = new List<string>
            {
                "root.cs",
                "subdir1/file1.cs",
                "subdir1/subdir2/file2.js",
                "subdir3/unsupported.txt",
            };

            var result = testableLister.InvokeConvertAndFilterPaths(relativePaths, _testRepoPath);

            Assert.HasCount(3, result, "Should include supported files from all directories");
            Assert.IsTrue(result.Any(p => p.EndsWith("root.cs")), "Should include root file");
            Assert.IsTrue(result.Any(p => p.Contains("subdir1") && p.EndsWith("file1.cs")), "Should include subdir1 file");
            Assert.IsTrue(result.Any(p => p.Contains("subdir2") && p.EndsWith("file2.js")), "Should include nested subdir file");

            testableLister.Dispose();
        }
    }
}
