// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

            var result = await _lister.CollectFilesFromRepoStateAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should detect one modified file");
            Assert.Contains(modifiedFile, result, "Should contain the modified file");
        }

        [TestMethod]
        public async Task PeriodicScanAsync_DetectsChangesAndFiresEvent()
        {
            var testableInstance = new TestableGitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);

            try
            {
                testableInstance.Initialize(_testRepoPath, _testRepoPath);

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
                testableInstance.Initialize(_testRepoPath, _testRepoPath);

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
        public async Task PeriodicScanAsync_WhenExceptionThrown_LogsWarning()
        {
            var testableInstance = new TestableGitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);

            try
            {
                testableInstance.Initialize(_testRepoPath, _testRepoPath);
                testableInstance.ThrowInGetAllChangedFilesAsync = true;

                await testableInstance.InvokePeriodicScanAsync();

                Assert.IsTrue(_fakeLogger.WarnMessages.Any(m => m.Contains("Error during periodic scan")), "Should log warning on exception");
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
    }
}
