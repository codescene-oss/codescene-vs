// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    public class GitIgnoreCacheBustingTestsBase : GitChangeObserverTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            base.Setup();
            OnAfterSetup();
        }

        protected virtual void OnAfterSetup()
        {
        }

        protected void SetupWithGitignoreSupport()
        {
            _gitChangeObserverCore?.Dispose();
            _fakeGitService = new FakeGitServiceWithGitignoreSupport(_testRepoPath);
            _gitChangeObserverCore = CreateGitChangeObserverCore();
            Thread.Sleep(500);
        }
    }

    [TestClass]
    public class GitIgnoreCacheBustingTests : GitIgnoreCacheBustingTestsBase
    {
        [TestMethod]
        public async Task FileAddedToGitignore_IsRemovedFromTracker()
        {
            var trackedFile = CreateFile("secrets.ts", "export const apiKey = 'secret';");
            await TriggerFileChangeAsync(trackedFile);
            AssertFileInTracker(trackedFile, true);

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "secrets.ts\n");

            await Task.Delay(200);

            AssertFileInTracker(trackedFile, false);
        }

        [TestMethod]
        public async Task MultipleFilesAddedToGitignore_AllRemovedFromTracker()
        {
            var file1 = CreateFile("config1.ts", "export const config = {};");
            var file2 = CreateFile("config2.ts", "export const config2 = {};");
            await TriggerFileChangeAsync(file1);
            await TriggerFileChangeAsync(file2);
            AssertFileInTracker(file1, true);
            AssertFileInTracker(file2, true);

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "config1.ts\nconfig2.ts\n");

            await Task.Delay(200);

            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);
        }

        [TestMethod]
        public async Task GitignoreInSubdirectory_RemovesMatchingFiles()
        {
            var subDir = Path.Combine(_testRepoPath, "src");
            Directory.CreateDirectory(subDir);

            var file1 = Path.Combine(subDir, "data.ts");
            File.WriteAllText(file1, "export const data = 123;");
            await TriggerFileChangeAsync(file1);
            AssertFileInTracker(file1, true);

            var gitignorePath = Path.Combine(subDir, ".gitignore");
            File.WriteAllText(gitignorePath, "data.ts\n");

            await Task.Delay(200);

            AssertFileInTracker(file1, false);
        }

        [TestMethod]
        public async Task GitignorePatternWithWildcard_RemovesMatchingFiles()
        {
            var file1 = CreateFile("test1.js", "console.log('test1');");
            var file2 = CreateFile("test2.js", "console.log('test2');");
            var file3 = CreateFile("app.ts", "export const test = 1;");
            await TriggerFileChangeAsync(file1);
            await TriggerFileChangeAsync(file2);
            await TriggerFileChangeAsync(file3);
            AssertFileInTracker(file1, true);
            AssertFileInTracker(file2, true);
            AssertFileInTracker(file3, true);

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "test*.js\n");

            await Task.Delay(200);

            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);
            AssertFileInTracker(file3, true);
        }

        [TestMethod]
        public async Task GitignoreDeleted_FileBecomesTrackedAgain()
        {
            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "ignored.ts\n");

            var ignoredFile = CreateFile("ignored.ts", "export const ignored = true;");
            await TriggerFileChangeAsync(ignoredFile);
            AssertFileInTracker(ignoredFile, false);

            File.Delete(gitignorePath);
            await Task.Delay(200);

            await TriggerFileChangeAsync(ignoredFile);
            AssertFileInTracker(ignoredFile, true);
        }

        [TestMethod]
        public async Task GitignoreModified_UpdatesTrackedFiles()
        {
            var file1 = CreateFile("temp1.ts", "export const temp1 = 1;");
            var file2 = CreateFile("temp2.ts", "export const temp2 = 2;");
            await TriggerFileChangeAsync(file1);
            await TriggerFileChangeAsync(file2);
            AssertFileInTracker(file1, true);
            AssertFileInTracker(file2, true);

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "temp1.ts\n");
            await Task.Delay(200);

            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, true);

            File.WriteAllText(gitignorePath, "temp2.ts\n");
            await Task.Delay(200);

            await TriggerFileChangeAsync(file1);
            AssertFileInTracker(file1, true);
            AssertFileInTracker(file2, false);
        }

        [TestMethod]
        public async Task NonIgnoredFiles_RemainInTrackerAfterGitignoreChange()
        {
            var ignoredFile = CreateFile("secret.py", "API_KEY = 'secret'");
            var normalFile = CreateFile("app.ts", "export const app = {};");
            await TriggerFileChangeAsync(ignoredFile);
            await TriggerFileChangeAsync(normalFile);
            AssertFileInTracker(ignoredFile, true);
            AssertFileInTracker(normalFile, true);

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "*.py\n");
            await Task.Delay(200);

            AssertFileInTracker(ignoredFile, false);
            AssertFileInTracker(normalFile, true);
        }

        [TestMethod]
        public async Task EmptyGitignore_DoesNotRemoveTrackedFiles()
        {
            var file1 = CreateFile("test.ts", "export const test = 1;");
            await TriggerFileChangeAsync(file1);
            AssertFileInTracker(file1, true);

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, string.Empty);
            await Task.Delay(200);

            AssertFileInTracker(file1, true);
        }

        protected override void OnAfterSetup()
        {
            SetupWithGitignoreSupport();
        }
    }

    [TestClass]
    public class TrackerManagerTests
    {
        private TrackerManager _trackerManager;

        [TestInitialize]
        public void Setup()
        {
            _trackerManager = new TrackerManager();
        }

        [TestMethod]
        public void GetAllTrackedFiles_ReturnsEmptyList_WhenNoFilesTracked()
        {
            var files = _trackerManager.GetAllTrackedFiles();
            Assert.IsNotNull(files);
            Assert.IsEmpty(files);
        }

        [TestMethod]
        public void GetAllTrackedFiles_ReturnsSingleFile_WhenOneFileTracked()
        {
            var filePath = @"C:\test\file.ts";
            _trackerManager.Add(filePath);

            var files = _trackerManager.GetAllTrackedFiles();
            Assert.HasCount(1, files);
            CollectionAssert.Contains(files, filePath);
        }

        [TestMethod]
        public void GetAllTrackedFiles_ReturnsAllFiles_WhenMultipleFilesTracked()
        {
            var file1 = @"C:\test\file1.ts";
            var file2 = @"C:\test\file2.ts";
            var file3 = @"C:\test\file3.ts";

            _trackerManager.Add(file1);
            _trackerManager.Add(file2);
            _trackerManager.Add(file3);

            var files = _trackerManager.GetAllTrackedFiles();
            Assert.HasCount(3, files);
            CollectionAssert.Contains(files, file1);
            CollectionAssert.Contains(files, file2);
            CollectionAssert.Contains(files, file3);
        }

        [TestMethod]
        public void GetAllTrackedFiles_ReturnsSnapshot_NotLiveReference()
        {
            var file1 = @"C:\test\file1.ts";
            _trackerManager.Add(file1);

            var files1 = _trackerManager.GetAllTrackedFiles();
            var file2 = @"C:\test\file2.ts";
            _trackerManager.Add(file2);
            var files2 = _trackerManager.GetAllTrackedFiles();

            Assert.HasCount(1, files1);
            Assert.HasCount(2, files2);
        }

        [TestMethod]
        public void GetAllTrackedFiles_DoesNotIncludeRemovedFiles()
        {
            var file1 = @"C:\test\file1.ts";
            var file2 = @"C:\test\file2.ts";

            _trackerManager.Add(file1);
            _trackerManager.Add(file2);
            _trackerManager.Remove(file1);

            var files = _trackerManager.GetAllTrackedFiles();
            Assert.HasCount(1, files);
            CollectionAssert.DoesNotContain(files, file1);
            CollectionAssert.Contains(files, file2);
        }

        [TestMethod]
        public void GetAllTrackedFiles_HandlesAddingSameFileTwice()
        {
            var filePath = @"C:\test\file.ts";
            _trackerManager.Add(filePath);
            _trackerManager.Add(filePath);

            var files = _trackerManager.GetAllTrackedFiles();
            Assert.HasCount(1, files);
        }
    }

    [TestClass]
    public class GitIgnoreWatcherTests
    {
        private string _testDir;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"gitignore-watcher-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDir))
            {
                try
                {
                    Directory.Delete(_testDir, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public async Task GitIgnoreWatcher_FiresEvent_WhenGitignoreCreated()
        {
            var eventFired = false;
            var watcher = new GitIgnoreWatcher(_testDir, null);
            watcher.GitIgnoreChanged += (sender, args) => eventFired = true;

            await Task.Delay(100);

            var gitignorePath = Path.Combine(_testDir, ".gitignore");
            File.WriteAllText(gitignorePath, "*.log\n");

            var success = await WaitForConditionAsync(() => eventFired, 2000);
            watcher.Dispose();

            Assert.IsTrue(success, "GitIgnoreChanged event should have fired");
        }

        [TestMethod]
        public async Task GitIgnoreWatcher_FiresEvent_WhenGitignoreModified()
        {
            var gitignorePath = Path.Combine(_testDir, ".gitignore");
            File.WriteAllText(gitignorePath, "*.log\n");

            var eventFired = false;
            var watcher = new GitIgnoreWatcher(_testDir, null);
            watcher.GitIgnoreChanged += (sender, args) => eventFired = true;

            await Task.Delay(100);

            File.AppendAllText(gitignorePath, "*.tmp\n");

            var success = await WaitForConditionAsync(() => eventFired, 2000);
            watcher.Dispose();

            Assert.IsTrue(success, "GitIgnoreChanged event should have fired");
        }

        [TestMethod]
        public async Task GitIgnoreWatcher_FiresEvent_WhenGitignoreDeleted()
        {
            var gitignorePath = Path.Combine(_testDir, ".gitignore");
            File.WriteAllText(gitignorePath, "*.log\n");

            var eventFired = false;
            var watcher = new GitIgnoreWatcher(_testDir, null);
            watcher.GitIgnoreChanged += (sender, args) => eventFired = true;

            await Task.Delay(100);

            File.Delete(gitignorePath);

            var success = await WaitForConditionAsync(() => eventFired, 2000);
            watcher.Dispose();

            Assert.IsTrue(success, "GitIgnoreChanged event should have fired");
        }

        [TestMethod]
        public async Task GitIgnoreWatcher_FiresEvent_WhenGitignoreInSubdirectoryCreated()
        {
            var subDir = Path.Combine(_testDir, "src");
            Directory.CreateDirectory(subDir);

            var eventFired = false;
            var watcher = new GitIgnoreWatcher(_testDir, null);
            watcher.GitIgnoreChanged += (sender, args) => eventFired = true;

            await Task.Delay(100);

            var gitignorePath = Path.Combine(subDir, ".gitignore");
            File.WriteAllText(gitignorePath, "*.test.ts\n");

            var success = await WaitForConditionAsync(() => eventFired, 2000);
            watcher.Dispose();

            Assert.IsTrue(success, "GitIgnoreChanged event should have fired for subdirectory .gitignore");
        }

        [TestMethod]
        public void GitIgnoreWatcher_DisposesCleanly()
        {
            var watcher = new GitIgnoreWatcher(_testDir, null);
            watcher.Dispose();
            watcher.Dispose();
        }

        [TestMethod]
        public void GitIgnoreWatcher_HandlesNullLogger()
        {
            var watcher = new GitIgnoreWatcher(_testDir, null);
            Assert.IsNotNull(watcher);
            watcher.Dispose();
        }

        [TestMethod]
        public void GitIgnoreWatcher_HandlesNonExistentDirectory()
        {
            var nonExistentDir = Path.Combine(_testDir, "nonexistent");
            var watcher = new GitIgnoreWatcher(nonExistentDir, null);
            Assert.IsNotNull(watcher);
            watcher.Dispose();
        }

        [TestMethod]
        public void GitIgnoreWatcher_HandlesNullDirectory()
        {
            var watcher = new GitIgnoreWatcher(null, null);
            Assert.IsNotNull(watcher);
            watcher.Dispose();
        }

        [TestMethod]
        public async Task GitIgnoreWatcher_ThrowingHandler_LogsWarningAndDoesNotCrash()
        {
            var mockLogger = new Mock<ILogger>();
            var watcher = new GitIgnoreWatcher(_testDir, mockLogger.Object);
            watcher.GitIgnoreChanged += (sender, args) => throw new InvalidOperationException("handler error");

            await Task.Delay(100);

            var gitignorePath = Path.Combine(_testDir, ".gitignore");
            File.WriteAllText(gitignorePath, "*.log\n");

            await WaitForConditionAsync(
                () => mockLogger.Invocations.Any(i => i.Method.Name == "Warn"),
                2000);
            watcher.Dispose();

            mockLogger.Verify(
                l => l.Warn(It.Is<string>(s => s.Contains("Error in GitIgnoreChanged handler")), It.IsAny<bool>()),
                Times.AtLeastOnce);
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

            return false;
        }
    }
}
