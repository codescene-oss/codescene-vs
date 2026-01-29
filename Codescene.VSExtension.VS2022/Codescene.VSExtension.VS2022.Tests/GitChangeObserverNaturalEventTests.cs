using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.VS2022.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Tests
{
    [TestClass]
    public class GitChangeObserverNaturalEventTests
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
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-natural-{Guid.NewGuid()}");

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
            var observer = new GitChangeObserver(_fakeLogger, _fakeCodeReviewer,
                _fakeSupportedFileChecker, _fakeGitService);

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
                LibGit2Sharp.Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }

            return filePath;
        }

        private async Task<bool> WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000, int pollIntervalMs = 100)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return true;
                }
                await Task.Delay(pollIntervalMs);
            }
            return condition();
        }

        [TestMethod]
        public async Task NaturalEvents_FilesEventuallyTracked()
        {
            _gitChangeObserver.Start();
            await Task.Delay(1500);

            var file1 = CreateFile("queued1.ts", "export const a = 1;");
            var file2 = CreateFile("queued2.ts", "export const b = 2;");

            await Task.Delay(100);

            var file1Tracked = await WaitForConditionAsync(() =>
                _gitChangeObserver.GetTrackerManager().Contains(file1), 5000);
            var file2Tracked = await WaitForConditionAsync(() =>
                _gitChangeObserver.GetTrackerManager().Contains(file2), 5000);

            Assert.IsTrue(file1Tracked, "File1 should eventually be tracked");
            Assert.IsTrue(file2Tracked, "File2 should eventually be tracked");
        }

        [TestMethod]
        public async Task NaturalEvents_FileModification_DetectedAndTracked()
        {
            _gitChangeObserver.Start();
            await Task.Delay(1500);

            var file = CreateFile("modify-test.ts", "export const x = 1;");

            var trackerManager = _gitChangeObserver.GetTrackerManager();
            var fileTracked = await WaitForConditionAsync(() => trackerManager.Contains(file), 5000);
            Assert.IsTrue(fileTracked, "File should be tracked after creation");

            File.WriteAllText(file, "export const x = 2;");

            await Task.Delay(2000);

            var stillTracked = trackerManager.Contains(file);
            Assert.IsTrue(stillTracked, "File should still be tracked after modification");
        }
    }
}
