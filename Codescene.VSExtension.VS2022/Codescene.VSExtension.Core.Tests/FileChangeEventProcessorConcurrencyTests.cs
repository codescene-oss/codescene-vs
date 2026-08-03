// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Enums.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class FileChangeEventProcessorConcurrencyTests
    {
        [TestMethod]
        public async Task ProcessQueuedEvents_WhenAlreadyProcessing_SkipsTimerTick()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();
            var getChangedFilesTcs = new TaskCompletionSource<List<string>>();

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct, string baselineCommit)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles(string baselineCommit) => getChangedFilesTcs.Task;

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "first.cs"));
                processor.Start(TimeSpan.FromMilliseconds(50), CancellationToken.None);

                await Task.Delay(100);

                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "second.cs"));

                await Task.Delay(100);

                getChangedFilesTcs.SetResult(new List<string> { "file.cs" });

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 2 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(2, processedEvents);
            var paths = processedEvents.Select(e => e.FilePath).OrderBy(p => p).ToList();
            Assert.AreEqual("first.cs", paths[0]);
            Assert.AreEqual("second.cs", paths[1]);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_AfterProcessingCompletes_ProcessesNewEvents()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct, string baselineCommit)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles(string baselineCommit) => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "first.cs"));
                processor.Start(TimeSpan.FromMilliseconds(50), CancellationToken.None);

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 1 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }

                Assert.HasCount(1, processedEvents);

                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "second.cs"));

                deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 2 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(2, processedEvents);
            Assert.AreEqual("first.cs", processedEvents[0].FilePath);
            Assert.AreEqual("second.cs", processedEvents[1].FilePath);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_WhenProcessingThrows_FlagIsReset()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();
            var shouldThrow = true;

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct, string baselineCommit)
            {
                if (shouldThrow)
                {
                    shouldThrow = false;
                    throw new InvalidOperationException("simulated error");
                }

                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles(string baselineCommit) => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "first.cs"));
                processor.Start(TimeSpan.FromMilliseconds(50), CancellationToken.None);

                await Task.Delay(200);

                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "second.cs"));

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 1 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(1, processedEvents);
            Assert.AreEqual("second.cs", processedEvents[0].FilePath);
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_WhenTickDelayed_ReusesCachedBaselineCommit()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();
            var getChangedFilesTcs = new TaskCompletionSource<List<string>>();
            var baselineCommitCallCount = 0;

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct, string baselineCommit)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles(string baselineCommit) => getChangedFilesTcs.Task;

            string GetBaselineCommit()
            {
                Interlocked.Increment(ref baselineCommitCallCount);
                return "abc123";
            }

            using (var processor = new FileChangeEventProcessor(
                logger,
                taskScheduler,
                ProcessEvent,
                GetChangedFiles,
                getBaselineCommitCallback: GetBaselineCommit))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "first.cs"));
                processor.Start(TimeSpan.FromMilliseconds(50), CancellationToken.None);

                await Task.Delay(100);

                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "second.cs"));

                await Task.Delay(100);

                getChangedFilesTcs.SetResult(new List<string> { "file.cs" });

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 2 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(2, processedEvents);
            Assert.AreEqual(1, baselineCommitCallCount, "GetBaselineCommit should only be called once due to caching");
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_WhenTickNotDelayed_ComputesFreshBaselineCommit()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();
            var baselineCommitCallCount = 0;

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct, string baselineCommit)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles(string baselineCommit) => Task.FromResult(new List<string>());

            string GetBaselineCommit()
            {
                Interlocked.Increment(ref baselineCommitCallCount);
                return "abc123";
            }

            using (var processor = new FileChangeEventProcessor(
                logger,
                taskScheduler,
                ProcessEvent,
                GetChangedFiles,
                getBaselineCommitCallback: GetBaselineCommit))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "first.cs"));
                processor.Start(TimeSpan.FromMilliseconds(50), CancellationToken.None);

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 1 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }

                await Task.Delay(100);

                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "second.cs"));

                deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 2 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(2, processedEvents);
            Assert.AreEqual(2, baselineCommitCallCount, "GetBaselineCommit should be called twice for non-delayed ticks");
        }

        [TestMethod]
        public async Task ProcessQueuedEvents_WhenGetChangedFilesThrows_ResetsProcessingFlagAndContinues()
        {
            var logger = new FakeLogger();
            var taskScheduler = new FakeAsyncTaskScheduler();
            var processedEvents = new List<FileChangeEvent>();
            var shouldThrow = true;

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken ct, string baselineCommit)
            {
                processedEvents.Add(evt);
                return Task.CompletedTask;
            }

            Task<List<string>> GetChangedFiles(string baselineCommit)
            {
                if (shouldThrow)
                {
                    shouldThrow = false;
                    throw new InvalidOperationException("simulated GetChangedFiles error");
                }

                return Task.FromResult(new List<string>());
            }

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "first.cs"));
                processor.Start(TimeSpan.FromMilliseconds(50), CancellationToken.None);

                await Task.Delay(200);

                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Change, "second.cs"));

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (processedEvents.Count < 1 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
            }

            Assert.HasCount(1, processedEvents);
            Assert.AreEqual("second.cs", processedEvents[0].FilePath);

            bool IsExpectedError((string Message, Exception Ex) entry) =>
                entry.Message.Contains("Error processing queued events")
                && entry.Ex != null
                && entry.Ex.Message.Contains("simulated GetChangedFiles error");

            Assert.IsTrue(logger.SnapshotErrorMessages().Any(IsExpectedError), "Expected outer error handler to log 'Error processing queued events'");
        }
    }
}
