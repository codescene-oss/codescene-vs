// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Enums.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverEventQueueTests : GitChangeObserverTestBase
    {
        [TestMethod]
        public async Task EventsAreQueued_InsteadOfProcessedImmediately()
        {
            _gitChangeObserverCore.Start();
            await Task.Delay(1000);

            var fileWatcher = _gitChangeObserverCore.FileWatcher;
            fileWatcher.EnableRaisingEvents = false;

            var file1 = CreateFile("queued1.ts", "export const a = 1;");
            var file2 = CreateFile("queued2.ts", "export const b = 2;");

            var queue = _gitChangeObserverCore.EventQueue;

            var event1 = new FileChangeEvent(FileChangeType.Create, file1);
            var event2 = new FileChangeEvent(FileChangeType.Create, file2);

            queue.Enqueue(event1);
            queue.Enqueue(event2);

            Assert.HasCount(2, queue, "Events should be queued");
            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);

            var queueEmptied = await WaitForConditionAsync(() => queue.Count == 0, 5000);
            Assert.IsTrue(queueEmptied, "Queue should be empty after processing");

            var trackerManager = _gitChangeObserverCore.GetTrackerManager();
            var file1InTracker = await WaitForConditionAsync(() => trackerManager.Contains(file1), 5000);
            var file2InTracker = await WaitForConditionAsync(() => trackerManager.Contains(file2), 5000);

            Assert.IsTrue(file1InTracker, "File1 should be in tracker after processing");
            Assert.IsTrue(file2InTracker, "File2 should be in tracker after processing");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_CalledOncePerBatch_NotPerFile()
        {
            var observer = new TestableGitChangeObserverCore(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                new FakeAsyncTaskScheduler(),
                _fakeGitChangeLister);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            observer.Start();
            await Task.Delay(1000);

            var files = new[]
            {
                CreateFile("cache1.ts", "export const a = 1;"),
                CreateFile("cache2.ts", "export const b = 2;"),
                CreateFile("cache3.ts", "export const c = 3;"),
            };

            var queue = observer.EventQueue;

            observer.ResetCallCount();

            foreach (var file in files)
            {
                var evt = new FileChangeEvent(FileChangeType.Create, file);
                queue.Enqueue(evt);
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
            var observer = new TestableGitChangeObserverCore(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                new FakeAsyncTaskScheduler(),
                _fakeGitChangeLister);

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
            _gitChangeObserverCore.Start();

            Assert.IsNotNull(_gitChangeObserverCore.ScheduledTimer, "Scheduled timer should exist before disposal");

            _gitChangeObserverCore.Dispose();

            Assert.IsNull(_gitChangeObserverCore.ScheduledTimer, "Scheduled timer should be null after disposal");
        }

        [TestMethod]
        public void FilesDetected_WithAbsolutePaths_AddsCorrectPathToTracker()
        {
            var absolutePath = Path.Combine(_testRepoPath, "detected.ts");
            File.WriteAllText(absolutePath, "export const x = 1;");

            var detectedFiles = new HashSet<string> { absolutePath };

            _fakeGitChangeLister.SimulateFilesDetected(detectedFiles);

            var trackerManager = _gitChangeObserverCore.GetTrackerManager();
            Assert.IsTrue(
                trackerManager.Contains(absolutePath),
                $"Tracker should contain the absolute path '{absolutePath}'");
        }
    }
}
