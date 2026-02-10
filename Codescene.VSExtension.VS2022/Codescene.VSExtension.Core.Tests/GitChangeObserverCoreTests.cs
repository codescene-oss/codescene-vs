// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
    public class GitChangeObserverCoreTests : GitChangeObserverTestBase
    {
        [TestMethod]
        public async Task GetChangedFilesVsBaseline_ReturnsEmptyArray_ForCleanRepository()
        {
            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            Assert.IsEmpty(changedFiles, "Should return empty list for clean repository");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsNewUntrackedFiles()
        {
            CreateFile("test.ts", "console.log(\"test\");");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "test.ts");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsModifiedFiles()
        {
            var testFile = CommitFile("index.js", "console.log(\"hello\");", "Add index.js");
            File.WriteAllText(testFile, "console.log(\"modified\");");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "index.js");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsStagedFiles()
        {
            CreateFile("script.py", "print(\"hello\")");

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, "script.py");
            }

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "script.py");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_CombinesStatusAndDiffChanges()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var branch = repo.CreateBranch("feature-branch");
                LibGit2Sharp.Commands.Checkout(repo, branch);
            }

            CommitFile("committed.ts", "export const foo = 1;", "Add committed.ts");
            CreateFile("uncommitted.ts", "export const bar = 2;");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "committed.ts");
            AssertFileInChangedList(changedFiles, "uncommitted.ts");
        }

        [DataRow("notes.txt", "Some notes", false, DisplayName = "Rejects unsupported file types (.txt)")]
        [DataRow("code.ts", "export const x = 1;", true, DisplayName = "Accepts supported file types (.ts)")]
        [TestMethod]
        public async Task ShouldProcessFile_ChecksFileSupport(string filename, string content, bool expectedResult)
        {
            var filePath = CreateFile(filename, content);
            _fakeSupportedFileChecker.SetSupported(filePath, expectedResult);

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            var result = _gitChangeObserverCore.ShouldProcessFileForTesting(filePath, changedFiles);

            Assert.AreEqual(
                expectedResult,
                result,
                expectedResult ? $"Should process {Path.GetExtension(filename)} files" : $"Should not process {Path.GetExtension(filename)} files");
        }

        [TestMethod]
        public async Task HandleFileChange_FiltersFilesNotInChangedList()
        {
            var changedFile = CreateFile("changed.ts", "export const x = 1;");
            var committedFile = CommitFile("committed.js", "console.log(\"committed\");", "Add committed.js");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "changed.ts");
            AssertFileInChangedList(changedFiles, "committed.js", false);

            await TriggerFileChangeAsync(changedFile);
            await TriggerFileChangeAsync(committedFile);

            AssertFileInTracker(changedFile);
            AssertFileInTracker(committedFile, false);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_HandlesFilesWithWhitespaceInNames()
        {
            CreateFile("my file.ts", "console.log(\"has spaces\");");
            CreateFile("test file with spaces.js", "console.log(\"also has spaces\");");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            var fileNames = changedFiles.Select(f => Path.GetFileName(f)).ToList();

            Assert.Contains("my file.ts", fileNames, "Should include file with spaces: my file.ts");
            Assert.Contains("test file with spaces.js", fileNames, "Should include file with spaces: test file with spaces.js");
        }

        [TestMethod]
        public void Dispose_CleansUpFileWatcher()
        {
            Assert.IsNotNull(_gitChangeObserverCore.FileWatcher, "File watcher should exist");

            _gitChangeObserverCore.Dispose();
        }

        [TestMethod]
        public void OnGitChangeListerFilesDetected_AddsExistingFilesToTracker()
        {
            var existingFile = CreateFile("tracked.ts", "export const x = 1;");
            var files = new HashSet<string> { existingFile };

            _fakeGitChangeLister.SimulateFilesDetected(files);

            AssertFileInTracker(existingFile, true);
        }

        [TestMethod]
        public void OnGitChangeListerFilesDetected_SkipsNonExistentFiles()
        {
            var nonExistentFile = Path.Combine(_testRepoPath, "nonexistent.ts");
            var files = new HashSet<string> { nonExistentFile };

            _fakeGitChangeLister.SimulateFilesDetected(files);

            AssertFileInTracker(nonExistentFile, false);
        }

        [TestMethod]
        public async Task OnGitChangeListerFilesDetected_WhenExceptionThrown_LogsWarning()
        {
            _fakeLogger.WarnMessages.Clear();

            var eventHandler = typeof(GitChangeObserverCore).GetMethod("OnGitChangeListerFilesDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var exceptionThrown = false;
            try
            {
                eventHandler?.Invoke(_gitChangeObserverCore, new object[] { this, null });
            }
            catch
            {
                exceptionThrown = true;
            }

            Assert.IsFalse(exceptionThrown, "Exception should be caught and logged, not thrown");

            await WaitForConditionAsync(() => _fakeLogger.WarnMessages.Count > 0, 2000);

            Assert.IsNotEmpty(_fakeLogger.WarnMessages, "Should log warning when exception is thrown");
            Assert.IsTrue(
                _fakeLogger.WarnMessages.Exists(msg => msg.Contains("Error processing detected files")),
                "Warning should mention error processing detected files");
        }

        [TestMethod]
        public async Task InitializeTracker_SkipsNonExistentFiles()
        {
            var existingFile = CreateFile("existing.ts", "export const x = 1;");
            var nonExistentFile = Path.Combine(_testRepoPath, "nonexistent.ts");

            _fakeGitChangeLister.FilesToReturn = new HashSet<string> { existingFile, nonExistentFile };

            var newObserver = CreateGitChangeObserverCore();
            var newTracker = newObserver.GetTrackerManager();

            await WaitForConditionAsync(() => newTracker.Contains(existingFile), 2000);

            Assert.IsTrue(newTracker.Contains(existingFile), "Existing file should be in tracker");
            Assert.IsFalse(newTracker.Contains(nonExistentFile), "Non-existent file should not be in tracker");

            newObserver.Dispose();
        }

        [TestMethod]
        public async Task InitializeTracker_WhenExceptionThrown_LogsWarning()
        {
            _fakeLogger.WarnMessages.Clear();

            _fakeGitChangeLister.ThrowOnCollectFiles = true;

            var newObserver = CreateGitChangeObserverCore();

            await WaitForConditionAsync(() => _fakeLogger.WarnMessages.Count > 0, 2000);

            Assert.IsNotEmpty(_fakeLogger.WarnMessages, "Should log warning when CollectFilesFromRepoStateAsync throws");
            Assert.IsTrue(
                _fakeLogger.WarnMessages.Exists(msg => msg.Contains("Error initializing tracker")),
                "Warning should mention error initializing tracker");

            newObserver.Dispose();
        }
    }
}
