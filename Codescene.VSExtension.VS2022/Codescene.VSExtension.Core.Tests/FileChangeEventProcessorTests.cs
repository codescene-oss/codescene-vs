// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Enums.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class FileChangeEventProcessorTests
    {
        [TestMethod]
        public async Task ProcessQueuedEvents_InvokesProcessEventCallback()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processEventInvoked = false;
            FileChangeEvent capturedEvt = null;
            List<string> capturedChangedFiles = null;

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct)
            {
                processEventInvoked = true;
                capturedEvt = evt;
                capturedChangedFiles = changedFiles;
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string> { "file1.cs" });

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "test.cs"));
                processor.Start(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!processEventInvoked && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.IsTrue(processEventInvoked, "ProcessEvent callback should have been invoked within 5 seconds");
            Assert.IsNotNull(capturedEvt);
            Assert.AreEqual(FileChangeType.Change, capturedEvt.Type);
            Assert.AreEqual("test.cs", capturedEvt.FilePath);
            Assert.IsNotNull(capturedChangedFiles);
            Assert.HasCount(1, capturedChangedFiles);
            Assert.AreEqual("file1.cs", capturedChangedFiles[0]);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_WhenCallbackThrows_LogsWarning()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();

            bool IsExpectedWarning(string m) =>
                m.Contains("Error processing file change event") && m.Contains("simulated error");

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct) =>
                throw new InvalidOperationException("simulated error");

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Create, "test.cs"));
                processor.Start(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && !logger.WarnMessages.Any(IsExpectedWarning))
                {
                    await Task.Delay(50);
                }
            }

            Assert.IsTrue(logger.WarnMessages.Any(IsExpectedWarning), "Expected Warn for callback throw (ProcessOneEventAsync catch)");
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_CoalescesMultipleEventsForSamePath_LastEventWins()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Create, "same.cs"));
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "same.cs"));
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Delete, "same.cs"));
                processor.Start(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(1, processedEvents);
            Assert.AreEqual(FileChangeType.Delete, processedEvents[0].Type);
            Assert.AreEqual("same.cs", processedEvents[0].FilePath);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_CoalescesCreateAndChange_ToSingleChangeEvent()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Create, "new.cs"));
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "new.cs"));
                processor.Start(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(1, processedEvents);
            Assert.AreEqual(FileChangeType.Change, processedEvents[0].Type);
            Assert.AreEqual("new.cs", processedEvents[0].FilePath);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_DoesNotCoalesceDifferentPaths()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "a.cs"));
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "b.cs"));
                processor.Start(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 2 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(2, processedEvents);
            var paths = processedEvents.Select(e => e.FilePath).OrderBy(p => p).ToList();
            Assert.AreEqual("a.cs", paths[0]);
            Assert.AreEqual("b.cs", paths[1]);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_WhenCancellationRequested_ExitsWithoutProcessing()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processEventInvoked = false;

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct)
            {
                processEventInvoked = true;
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
                {
                    processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "test.cs"));
                    processor.Start(TimeSpan.FromMilliseconds(10), cts.Token);
                    await Task.Delay(350);
                }
            }

            Assert.IsFalse(processEventInvoked);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_WhenQueueEmpty_ExitsWithoutInvokingCallback()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processEventInvoked = false;

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct)
            {
                processEventInvoked = true;
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.Start(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                await Task.Delay(350);
            }

            Assert.IsFalse(processEventInvoked);
        }

        [TestMethod]
        public async Task ProcessOneEventAsync_WhenTokenCancelled_ExitsWithoutInvokingCallback()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var firstInvoked = false;
            var secondInvoked = false;

            using (var cts = new CancellationTokenSource())
            {
                Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct)
                {
                    if (evt.FilePath == "first.cs")
                    {
                        firstInvoked = true;
                        cts.Cancel();
                    }
                    else if (evt.FilePath == "second.cs")
                    {
                        secondInvoked = true;
                    }

                    return Task.CompletedTask;
                }

                Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

                using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
                {
                    processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "first.cs"));
                    processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "second.cs"));
                    processor.Start(TimeSpan.FromMilliseconds(10), cts.Token);
                    await Task.Delay(100);
                }
            }

            Assert.IsTrue(firstInvoked);
            Assert.IsFalse(secondInvoked);
        }
    }
}
