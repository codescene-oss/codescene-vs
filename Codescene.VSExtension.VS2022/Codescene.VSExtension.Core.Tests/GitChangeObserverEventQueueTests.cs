using Codescene.VSExtension.VS2022.Application.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class GitChangeObserverEventQueueTests
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
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-event-queue-{Guid.NewGuid()}");

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

        private void AssertFileInTracker(string filePath, bool shouldExist = true)
        {
            var trackerManager = _gitChangeObserver.GetTrackerManager();
            var exists = trackerManager.Contains(filePath);
            Assert.AreEqual(shouldExist, exists, shouldExist ? "File should be in tracker" : "File should not be in tracker");
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
        public async Task EventsAreQueued_InsteadOfProcessedImmediately()
        {
            _gitChangeObserver.Start();
            await Task.Delay(1000);

            var fileWatcherField = typeof(GitChangeObserver).GetField("_fileWatcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fileWatcher = (FileSystemWatcher)fileWatcherField.GetValue(_gitChangeObserver);
            fileWatcher.EnableRaisingEvents = false;

            var file1 = CreateFile("queued1.ts", "export const a = 1;");
            var file2 = CreateFile("queued2.ts", "export const b = 2;");

            var eventQueueField = typeof(GitChangeObserver).GetField("_eventQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = eventQueueField.GetValue(_gitChangeObserver);

            var fileChangeEventType = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeEvent");
            var fileChangeTypeEnum = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeType");

            var createType = Enum.Parse(fileChangeTypeEnum, "Create");
            var event1 = Activator.CreateInstance(fileChangeEventType, createType, file1);
            var event2 = Activator.CreateInstance(fileChangeEventType, createType, file2);

            var enqueueMethod = queue.GetType().GetMethod("Enqueue");
            enqueueMethod.Invoke(queue, new[] { event1 });
            enqueueMethod.Invoke(queue, new[] { event2 });

            var countProperty = queue.GetType().GetProperty("Count");
            Assert.AreEqual(2, (int)countProperty.GetValue(queue), "Events should be queued");
            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);

            var queueEmptied = await WaitForConditionAsync(() => (int)countProperty.GetValue(queue) == 0, 5000);
            Assert.IsTrue(queueEmptied, "Queue should be empty after processing");

            var trackerManager = _gitChangeObserver.GetTrackerManager();
            var file1InTracker = await WaitForConditionAsync(() => trackerManager.Contains(file1), 5000);
            var file2InTracker = await WaitForConditionAsync(() => trackerManager.Contains(file2), 5000);

            Assert.IsTrue(file1InTracker, "File1 should be in tracker after processing");
            Assert.IsTrue(file2InTracker, "File2 should be in tracker after processing");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_CalledOncePerBatch_NotPerFile()
        {
            var observer = new TestableGitChangeObserver();

            var loggerField = typeof(GitChangeObserver).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reviewerField = typeof(GitChangeObserver).GetField("_codeReviewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var checkerField = typeof(GitChangeObserver).GetField("_supportedFileChecker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gitServiceField = typeof(GitChangeObserver).GetField("_gitService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            loggerField?.SetValue(observer, _fakeLogger);
            reviewerField?.SetValue(observer, _fakeCodeReviewer);
            checkerField?.SetValue(observer, _fakeSupportedFileChecker);
            gitServiceField?.SetValue(observer, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            observer.Start();
            await Task.Delay(1000);

            var files = new[]
            {
                CreateFile("cache1.ts", "export const a = 1;"),
                CreateFile("cache2.ts", "export const b = 2;"),
                CreateFile("cache3.ts", "export const c = 3;")
            };

            var eventQueueField = typeof(GitChangeObserver).GetField("_eventQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = eventQueueField.GetValue(observer);

            var fileChangeEventType = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeEvent");
            var fileChangeTypeEnum = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeType");
            var createType = Enum.Parse(fileChangeTypeEnum, "Create");
            var enqueueMethod = queue.GetType().GetMethod("Enqueue");

            observer.ResetCallCount();

            foreach (var file in files)
            {
                var evt = Activator.CreateInstance(fileChangeEventType, createType, file);
                enqueueMethod.Invoke(queue, new[] { evt });
            }

            var initialCount = observer.GetChangedFilesCallCount;

            var methodCalled = await WaitForConditionAsync(() => observer.GetChangedFilesCallCount > initialCount, 5000);
            Assert.IsTrue(methodCalled, "GetChangedFilesVsBaselineAsync should be called after batch processing");

            Assert.AreEqual(initialCount + 1, observer.GetChangedFilesCallCount, "GetChangedFilesVsBaselineAsync should be called exactly once per batch");

            var trackerManager = observer.GetTrackerManager();
            foreach (var file in files)
            {
                var fileInTracker = await WaitForConditionAsync(() => trackerManager.Contains(file), 5000);
                Assert.IsTrue(fileInTracker, $"File {file} should be in tracker");
            }

            observer.Dispose();
        }

        [TestMethod]
        public async Task EmptyQueue_DoesNotTrigger_UnnecessaryProcessing()
        {
            var observer = new TestableGitChangeObserver();

            var loggerField = typeof(GitChangeObserver).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reviewerField = typeof(GitChangeObserver).GetField("_codeReviewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var checkerField = typeof(GitChangeObserver).GetField("_supportedFileChecker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gitServiceField = typeof(GitChangeObserver).GetField("_gitService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            loggerField?.SetValue(observer, _fakeLogger);
            reviewerField?.SetValue(observer, _fakeCodeReviewer);
            checkerField?.SetValue(observer, _fakeSupportedFileChecker);
            gitServiceField?.SetValue(observer, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            await Task.Delay(1000);

            observer.Start();
            await Task.Delay(500);

            observer.ResetCallCount();

            await Task.Delay(5000);

            Assert.AreEqual(0, observer.GetChangedFilesCallCount, "GetChangedFilesVsBaselineAsync should not be called when queue is empty");

            observer.Dispose();
        }

        [TestMethod]
        public void Dispose_CleansUpScheduledTimer()
        {
            _gitChangeObserver.Start();

            var timerField = typeof(GitChangeObserver).GetField("_scheduledTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(timerField.GetValue(_gitChangeObserver), "Scheduled timer should exist before disposal");

            _gitChangeObserver.Dispose();

            Assert.IsNull(timerField.GetValue(_gitChangeObserver), "Scheduled timer should be null after disposal");
        }
    }
}
