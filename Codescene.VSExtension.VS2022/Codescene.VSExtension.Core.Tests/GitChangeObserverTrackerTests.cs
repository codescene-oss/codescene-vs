using Codescene.VSExtension.VS2022.Application.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class GitChangeObserverTrackerTests
    {
        private string _testRepoPath;
        private GitChangeObserver _gitChangeObserver;
        private FakeLogger _fakeLogger;
        private FakeCodeReviewer _fakeCodeReviewer;
        private FakeSupportedFileChecker _fakeSupportedFileChecker;
        private FakeGitService _fakeGitService;
        private FakeSavedFilesTracker _fakeSavedFilesTracker;
        private FakeOpenFilesObserver _fakeOpenFilesObserver;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-observer-tracker-{Guid.NewGuid()}");

            if (Directory.Exists(_testRepoPath))
            {
                Directory.Delete(_testRepoPath, true);
            }

            Directory.CreateDirectory(_testRepoPath);

            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
                repo.Config.Set("advice.defaultBranchName", "false");
            }

            var gitInfoDir = Path.Combine(_testRepoPath, ".git", "info");
            Directory.CreateDirectory(gitInfoDir);
            var dummyExcludesPath = Path.Combine(gitInfoDir, "exclude-test");
            File.WriteAllText(dummyExcludesPath, "# Test excludes file - will not match anything\n__xxxxxxxxxxxxx__\n");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("core.excludesfile", dummyExcludesPath);
            }

            CommitFile("README.md", "# Test Repository", "Initial commit");

            _fakeLogger = new FakeLogger();
            _fakeCodeReviewer = new FakeCodeReviewer();
            _fakeSupportedFileChecker = new FakeSupportedFileChecker();
            _fakeGitService = new FakeGitService();
            _fakeSavedFilesTracker = new FakeSavedFilesTracker();
            _fakeOpenFilesObserver = new FakeOpenFilesObserver();

            _gitChangeObserver = CreateGitChangeObserver();

            Thread.Sleep(500);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _gitChangeObserver?.Dispose();

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            if (File.Exists(gitignorePath))
            {
                File.Delete(gitignorePath);
            }

            if (Directory.Exists(_testRepoPath))
            {
                try
                {
                    Directory.Delete(_testRepoPath, true);
                }
                catch
                {
                }
            }
        }

        private GitChangeObserver CreateGitChangeObserver()
        {
            var observer = new GitChangeObserver();

            var loggerField = typeof(GitChangeObserver).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reviewerField = typeof(GitChangeObserver).GetField("_codeReviewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var checkerField = typeof(GitChangeObserver).GetField("_supportedFileChecker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gitServiceField = typeof(GitChangeObserver).GetField("_gitService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            loggerField?.SetValue(observer, _fakeLogger);
            reviewerField?.SetValue(observer, _fakeCodeReviewer);
            checkerField?.SetValue(observer, _fakeSupportedFileChecker);
            gitServiceField?.SetValue(observer, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            return observer;
        }

        private string CreateFile(string filename, string content)
        {
            var filePath = Path.Combine(_testRepoPath, filename);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private string CommitFile(string filename, string content, string message)
        {
            var filePath = CreateFile(filename, content);

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }

            return filePath;
        }

        private TrackerManager GetTrackerManager()
        {
            return _gitChangeObserver.GetTrackerManager();
        }

        private async Task TriggerFileChangeAsync(string filePath)
        {
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            await _gitChangeObserver.HandleFileChangeForTestingAsync(filePath, changedFiles);
        }

        private void AssertFileInTracker(string filePath, bool shouldExist = true)
        {
            var trackerManager = GetTrackerManager();
            var exists = trackerManager.Contains(filePath);
            Assert.AreEqual(shouldExist, exists, shouldExist ? "File should be in tracker" : "File should not be in tracker");
        }

        [TestMethod]
        public async Task TrackerTracksAddedFiles()
        {
            var newFile = CreateFile("tracked.ts", "export const x = 1;");
            _gitChangeObserver.Start();
            await Task.Delay(500);

            await TriggerFileChangeAsync(newFile);

            AssertFileInTracker(newFile);
        }

        [TestMethod]
        public async Task RemoveFromTracker_RemovesFileFromTracking()
        {
            var newFile = CreateFile("removable.ts", "export const x = 1;");
            _gitChangeObserver.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(newFile);
            AssertFileInTracker(newFile);

            _gitChangeObserver.RemoveFromTracker(newFile);

            AssertFileInTracker(newFile, false);
        }

        [TestMethod]
        public async Task HandleFileDelete_RemovesTrackedFile()
        {
            var newFile = CreateFile("deletable.ts", "export const x = 1;");
            _gitChangeObserver.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(newFile);
            AssertFileInTracker(newFile);

            File.Delete(newFile);
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            await _gitChangeObserver.HandleFileDeleteForTestingAsync(newFile, changedFiles);

            AssertFileInTracker(newFile, false);
        }

        [TestMethod]
        public async Task HandleFileDelete_HandlesDirectoryDeletion()
        {
            var subDir = Path.Combine(_testRepoPath, "subdir");
            Directory.CreateDirectory(subDir);
            var file1 = Path.Combine(subDir, "file1.ts");
            var file2 = Path.Combine(subDir, "file2.ts");
            File.WriteAllText(file1, "export const a = 1;");
            File.WriteAllText(file2, "export const b = 2;");

            _gitChangeObserver.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(file1);
            await TriggerFileChangeAsync(file2);
            AssertFileInTracker(file1);
            AssertFileInTracker(file2);

            Directory.Delete(subDir, true);
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            await _gitChangeObserver.HandleFileDeleteForTestingAsync(subDir, changedFiles);

            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);
        }
    }
}
