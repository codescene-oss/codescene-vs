// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class DroppingScheduledExecutorDynamicIntervalTests : DroppingScheduledExecutorTestBase
{
    [TestMethod]
    public void GetInterval_ReturnsInitialInterval()
    {
        var interval = TimeSpan.FromSeconds(9);
        _executor = new DroppingScheduledExecutor(ct => Task.CompletedTask, CancellationToken.None, interval, _mockLogger.Object);

        Assert.AreEqual(interval, _executor.GetInterval());
    }

    [TestMethod]
    public void SetInterval_UpdatesInterval()
    {
        var initialInterval = TimeSpan.FromSeconds(9);
        var newInterval = TimeSpan.FromSeconds(20);
        _executor = new DroppingScheduledExecutor(ct => Task.CompletedTask, CancellationToken.None, initialInterval, _mockLogger.Object);

        _executor.SetInterval(newInterval);

        Assert.AreEqual(newInterval, _executor.GetInterval());
    }

    [TestMethod]
    public void SetInterval_CanSetSmallerInterval()
    {
        var initialInterval = TimeSpan.FromSeconds(9);
        var smallerInterval = TimeSpan.FromSeconds(5);
        _executor = new DroppingScheduledExecutor(ct => Task.CompletedTask, CancellationToken.None, initialInterval, _mockLogger.Object);

        _executor.SetInterval(smallerInterval);

        Assert.AreEqual(smallerInterval, _executor.GetInterval());
    }

    [TestMethod]
    public void SetInterval_WhenDisposed_DoesNothing()
    {
        var initialInterval = TimeSpan.FromSeconds(9);
        var newInterval = TimeSpan.FromSeconds(20);
        _executor = new DroppingScheduledExecutor(ct => Task.CompletedTask, CancellationToken.None, initialInterval, _mockLogger.Object);

        _executor.Dispose();
        _executor.SetInterval(newInterval);

        Assert.AreEqual(initialInterval, _executor.GetInterval());
    }

    [TestMethod]
    public void SetInterval_LogsIntervalUpdate()
    {
        var initialInterval = TimeSpan.FromSeconds(9);
        var newInterval = TimeSpan.FromSeconds(20);
        _executor = new DroppingScheduledExecutor(ct => Task.CompletedTask, CancellationToken.None, initialInterval, _mockLogger.Object);

        _executor.SetInterval(newInterval);

        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("interval updated to") && s.Contains("20"))), Times.Once);
    }

    [TestMethod]
    public async Task SetInterval_WhenRunning_RestartsTimerWithNewInterval()
    {
        var executionCount = 0;
        var initialInterval = TimeSpan.FromMilliseconds(100);
        var newInterval = TimeSpan.FromMilliseconds(50);
        var thirdExecutionSignal = new TaskCompletionSource<bool>();

        _executor = new DroppingScheduledExecutor(
            async ct =>
            {
                await Task.CompletedTask;
                executionCount++;
                if (executionCount >= 3)
                {
                    thirdExecutionSignal.TrySetResult(true);
                }
            },
            CancellationToken.None,
            initialInterval,
            _mockLogger.Object);

        _executor.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        Assert.IsGreaterThanOrEqualTo(executionCount, 1);

        _executor.SetInterval(newInterval);
        Assert.AreEqual(newInterval, _executor.GetInterval());

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(thirdExecutionSignal.Task, timeoutTask);
        Assert.AreNotEqual(timeoutTask, completedTask, "Executor should continue running after interval change");
    }

    [TestMethod]
    public void SetInterval_WhenNotStarted_UpdatesIntervalWithoutStartingTimer()
    {
        var initialInterval = TimeSpan.FromSeconds(9);
        var newInterval = TimeSpan.FromSeconds(20);
        _executor = new DroppingScheduledExecutor(ct => Task.CompletedTask, CancellationToken.None, initialInterval, _mockLogger.Object);

        _executor.SetInterval(newInterval);

        Assert.AreEqual(newInterval, _executor.GetInterval());
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("started"))), Times.Never);
    }
}
