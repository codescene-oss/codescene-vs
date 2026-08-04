// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CpuUsageThrottlerTests
    {
        private Mock<ILogger> _mockLogger;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [TestMethod]
        public async Task WaitForCpuAsync_WhenCpuNotBusy_CompletesImmediately()
        {
            var cpuCheckCalls = 0;
            var throttler = new CpuUsageThrottler(
                _mockLogger.Object,
                () =>
                {
                    cpuCheckCalls++;
                    return Task.FromResult(false);
                },
                (ms, ct) => Task.Delay(ms, ct));

            await throttler.WaitForCpuAsync(CancellationToken.None);

            Assert.AreEqual(1, cpuCheckCalls);
            _mockLogger.Verify(x => x.Info(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task WaitForCpuAsync_WhenCpuBusyThenFree_WaitsAndRetries()
        {
            var responses = new Queue<bool>(new[] { true, false });
            var delayCallCount = 0;
            var delayMs = new List<int>();
            var throttler = new CpuUsageThrottler(
                _mockLogger.Object,
                () => Task.FromResult(responses.Dequeue()),
                (ms, ct) =>
                {
                    delayCallCount++;
                    delayMs.Add(ms);
                    return Task.CompletedTask;
                });

            await throttler.WaitForCpuAsync(CancellationToken.None);

            Assert.AreEqual(1, delayCallCount);
            Assert.AreEqual(9000, delayMs[0]);
            _mockLogger.Verify(x => x.Info(It.Is<string>(s => s.Contains("CPU too busy"))), Times.Once);
        }

        [TestMethod]
        public async Task WaitForCpuAsync_WhenCpuBusyMultipleTimes_WaitsMultipleTimes()
        {
            var responses = new Queue<bool>(new[] { true, true, true, false });
            var delayCallCount = 0;
            var throttler = new CpuUsageThrottler(
                _mockLogger.Object,
                () => Task.FromResult(responses.Dequeue()),
                (ms, ct) =>
                {
                    delayCallCount++;
                    return Task.CompletedTask;
                });

            await throttler.WaitForCpuAsync(CancellationToken.None);

            Assert.AreEqual(3, delayCallCount);
            _mockLogger.Verify(x => x.Info(It.Is<string>(s => s.Contains("CPU too busy"))), Times.Exactly(3));
        }

        [TestMethod]
        public async Task WaitForCpuAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            var responses = new Queue<bool>(new[] { true, true, true, false });
            var cts = new CancellationTokenSource();
            var throttler = new CpuUsageThrottler(
                _mockLogger.Object,
                () => Task.FromResult(responses.Dequeue()),
                (ms, ct) =>
                {
                    cts.Cancel();
                    return Task.CompletedTask;
                });

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => throttler.WaitForCpuAsync(cts.Token));
        }

        [TestMethod]
        public async Task NoOpCpuUsageThrottler_CompletesImmediately()
        {
            var throttler = new NoOpCpuUsageThrottler();

            var task = throttler.WaitForCpuAsync(CancellationToken.None);

            Assert.IsTrue(task.IsCompleted);
            await task;
        }
    }
}
