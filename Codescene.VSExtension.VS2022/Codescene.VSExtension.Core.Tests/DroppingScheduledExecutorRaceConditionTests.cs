// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using System.Threading;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class DroppingScheduledExecutorRaceConditionTests
{
    private Mock<ILogger> _mockLogger;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [TestMethod]
    public async Task Stop_ActionExecutesAfterStopReturns_RaceCondition()
    {
        long stopReturnTime;
        var executionStartTimes = new List<long>();
        var executionTimesLock = new object();

        Func<CancellationToken, Task> action = async ct =>
        {
            var startTime = Stopwatch.GetTimestamp();
            lock (executionTimesLock)
            {
                executionStartTimes.Add(startTime);
            }

            await Task.Delay(5);
        };

        using (var executor = new DroppingScheduledExecutor(action, CancellationToken.None, TimeSpan.FromMilliseconds(10), _mockLogger.Object))
        {
            executor.Start();

            await Task.Delay(50);

            executor.Stop();
            stopReturnTime = Stopwatch.GetTimestamp();

            await Task.Delay(100);
        }

        lock (executionTimesLock)
        {
            var executionsAfterStop = executionStartTimes.FindAll(t => t > stopReturnTime);
            Assert.IsLessThanOrEqualTo(
                1,
                executionsAfterStop.Count,
                $"Bug detected: {executionsAfterStop.Count} action(s) started executing after Stop() returned (at most one in-flight allowed)");
        }
    }

    [TestMethod]
    public async Task Stop_ActionExecutesAfterStopReturns_AggressiveRaceCondition()
    {
        var failures = 0;
        var iterations = 10;

        for (int i = 0; i < iterations; i++)
        {
            long stopReturnTime;
            var executionStartTimes = new List<long>();
            var executionTimesLock = new object();

            Func<CancellationToken, Task> action = async ct =>
            {
                var startTime = Stopwatch.GetTimestamp();
                lock (executionTimesLock)
                {
                    executionStartTimes.Add(startTime);
                }

                await Task.Delay(5);
            };

            using (var executor = new DroppingScheduledExecutor(action, CancellationToken.None, TimeSpan.FromMilliseconds(5), _mockLogger.Object))
            {
                executor.Start();

                await Task.Delay(30);

                executor.Stop();
                stopReturnTime = Stopwatch.GetTimestamp();

                await Task.Delay(50);
            }

            lock (executionTimesLock)
            {
                var executionsAfterStop = executionStartTimes.FindAll(t => t > stopReturnTime);
                if (executionsAfterStop.Count > 1)
                {
                    failures++;
                }
            }
        }

        Assert.AreEqual(
            0,
            failures,
            $"Bug detected: {failures}/{iterations} iterations had more than one action execute after Stop() returned");
    }

    [TestMethod]
    public async Task Dispose_ActionExecutesAfterDisposeReturns_RaceCondition()
    {
        var executionStartTimes = new List<long>();
        var executionTimesLock = new object();

        Func<CancellationToken, Task> action = async ct =>
        {
            var startTime = Stopwatch.GetTimestamp();
            lock (executionTimesLock)
            {
                executionStartTimes.Add(startTime);
            }

            await Task.Delay(5);
        };

        var executor = new DroppingScheduledExecutor(action, CancellationToken.None, TimeSpan.FromMilliseconds(10), _mockLogger.Object);
        executor.Start();

        await Task.Delay(50);

        executor.Dispose();
        var disposeReturnTime = Stopwatch.GetTimestamp();

        await Task.Delay(100);

        lock (executionTimesLock)
        {
            var executionsAfterDispose = executionStartTimes.FindAll(t => t > disposeReturnTime);
            Assert.IsLessThanOrEqualTo(
                1,
                executionsAfterDispose.Count,
                $"Bug detected: {executionsAfterDispose.Count} action(s) started executing after Dispose() returned (at most one in-flight allowed)");
        }
    }

    [TestMethod]
    public async Task Dispose_ActionExecutesAfterDisposeReturns_AggressiveRaceCondition()
    {
        var failures = 0;
        var iterations = 10;

        for (int i = 0; i < iterations; i++)
        {
            var executionStartTimes = new List<long>();
            var executionTimesLock = new object();

            Func<CancellationToken, Task> action = async ct =>
            {
                var startTime = Stopwatch.GetTimestamp();
                lock (executionTimesLock)
                {
                    executionStartTimes.Add(startTime);
                }

                await Task.Delay(5);
            };

            var executor = new DroppingScheduledExecutor(action, CancellationToken.None, TimeSpan.FromMilliseconds(5), _mockLogger.Object);
            executor.Start();

            await Task.Delay(30);

            executor.Dispose();
            var disposeReturnTime = Stopwatch.GetTimestamp();

            await Task.Delay(50);

            lock (executionTimesLock)
            {
                var executionsAfterDispose = executionStartTimes.FindAll(t => t > disposeReturnTime);
                if (executionsAfterDispose.Count > 1)
                {
                    failures++;
                }
            }
        }

        Assert.AreEqual(
            0,
            failures,
            $"Bug detected: {failures}/{iterations} iterations had more than one action execute after Dispose() returned");
    }
}
