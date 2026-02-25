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

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles)
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
                processor.Start(TimeSpan.FromMilliseconds(10));
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

            Task ProcessEvent(FileChangeEvent evt, List<string> changedFiles) =>
                throw new InvalidOperationException("simulated error");

            Task<List<string>> GetChangedFiles() => Task.FromResult(new List<string>());

            using (var processor = new FileChangeEventProcessor(logger, taskScheduler, ProcessEvent, GetChangedFiles))
            {
                processor.EnqueueEvent(new FileChangeEvent(FileChangeType.Create, "test.cs"));
                processor.Start(TimeSpan.FromMilliseconds(10));
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && !logger.WarnMessages.Any(IsExpectedWarning))
                {
                    await Task.Delay(20);
                }
            }

            Assert.IsTrue(logger.WarnMessages.Any(IsExpectedWarning));
        }
    }
}
