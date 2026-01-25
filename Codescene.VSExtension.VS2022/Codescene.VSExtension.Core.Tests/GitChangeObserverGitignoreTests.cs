using Codescene.VSExtension.VS2022.Application.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class GitChangeObserverGitignoreTests
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
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-gitignore-{Guid.NewGuid()}");

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

        private HashSet<string> GetTracker()
        {
            var trackerField = typeof(GitChangeObserver).GetField("_tracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (HashSet<string>)trackerField?.GetValue(_gitChangeObserver);
        }

        private async Task TriggerFileChangeAsync(string filePath)
        {
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            var handleFileChangeMethod = typeof(GitChangeObserver).GetMethod("HandleFileChangeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)handleFileChangeMethod?.Invoke(_gitChangeObserver, new object[] { filePath, changedFiles });
            await task;
        }

        private void AssertFileInChangedList(List<string> changedFiles, string filename, bool shouldExist = true)
        {
            var exists = changedFiles.Any(f => f.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(shouldExist, exists, shouldExist ? $"Should include {filename}" : $"Should not include {filename}");
        }

        private void AssertFileInTracker(string filePath, bool shouldExist = true)
        {
            var tracker = GetTracker();
            var exists = tracker.Contains(filePath);
            Assert.AreEqual(shouldExist, exists, shouldExist ? "File should be in tracker" : "File should not be in tracker");
        }

        [TestMethod]
        public async Task GitIgnoredFiles_AreNotTracked()
        {
            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "*.ignored\n");

            var ignoredFile = CreateFile("secret.ignored", "export const secret = \"hidden\";");

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "secret.ignored", false);

            await TriggerFileChangeAsync(ignoredFile);

            AssertFileInTracker(ignoredFile, false);

            File.Delete(gitignorePath);
        }

        [TestMethod]
        public async Task FileBecomesTracked_AfterGitignoreRemoval()
        {
            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "config.ts\n");

            var ignoredFile = CreateFile("config.ts", "export const config = { secret: true };");

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "config.ts", false);

            await TriggerFileChangeAsync(ignoredFile);
            AssertFileInTracker(ignoredFile, false);

            File.Delete(gitignorePath);
            await Task.Delay(100);

            changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "config.ts");

            await TriggerFileChangeAsync(ignoredFile);

            AssertFileInTracker(ignoredFile);
        }
    }
}
