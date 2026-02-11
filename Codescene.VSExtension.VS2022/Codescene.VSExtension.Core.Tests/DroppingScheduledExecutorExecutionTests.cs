// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class DroppingScheduledExecutorExecutionTests : DroppingScheduledExecutorTestBase
{
    [TestMethod]
    public async Task Start_ExecutesActionAfterInterval()
    {
        var actionExecutedSignal = new TaskCompletionSource<bool>();
        var interval = TimeSpan.FromMilliseconds(100);

        _executor = new DroppingScheduledExecutor(
            async () =>
            {
                await Task.CompletedTask;
                actionExecutedSignal.TrySetResult(true);
            },
            interval,
            _mockLogger.Object);

        _executor.Start();

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(actionExecutedSignal.Task, timeoutTask);

        Assert.AreNotEqual(timeoutTask, completedTask, "Action was not executed within timeout");
        Assert.IsTrue(await actionExecutedSignal.Task);
    }

    [TestMethod]
    public async Task Stop_WhenStarted_StopsTimer()
    {
        var executionCount = 0;
        var interval = TimeSpan.FromMilliseconds(100);

        _executor = new DroppingScheduledExecutor(
            async () =>
            {
                await Task.CompletedTask;
                executionCount++;
            },
            interval,
            _mockLogger.Object);

        _executor.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        _executor.Stop();

        var countAfterStop = executionCount;
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Assert.AreEqual(countAfterStop, executionCount, "Action should not execute after stop");
    }

    [TestMethod]
    public async Task OnTimerCallback_DropsExecution_WhenPreviousStillRunning()
    {
        var firstExecutionStarted = new TaskCompletionSource<bool>();
        var firstExecutionCanComplete = new TaskCompletionSource<bool>();
        var executionCount = 0;
        var interval = TimeSpan.FromMilliseconds(50);

        _executor = new DroppingScheduledExecutor(
            async () =>
            {
                executionCount++;
                if (executionCount == 1)
                {
                    firstExecutionStarted.TrySetResult(true);
                    await firstExecutionCanComplete.Task;
                }
            },
            interval,
            _mockLogger.Object);

        _executor.Start();

        await firstExecutionStarted.Task;
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        firstExecutionCanComplete.TrySetResult(true);
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        _mockLogger.Verify(
            l => l.Debug(It.Is<string>(s => s.Contains("dropping execution"))),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task OnTimerCallback_LogsError_WhenActionThrows()
    {
        var actionExecutedSignal = new TaskCompletionSource<bool>();
        var testException = new InvalidOperationException("Test exception");
        var interval = TimeSpan.FromMilliseconds(100);

        _executor = new DroppingScheduledExecutor(
            async () =>
            {
                await Task.CompletedTask;
                actionExecutedSignal.TrySetResult(true);
                throw testException;
            },
            interval,
            _mockLogger.Object);

        _executor.Start();

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(actionExecutedSignal.Task, timeoutTask);

        Assert.AreNotEqual(timeoutTask, completedTask, "Action was not executed within timeout");
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        _executor.Stop();

        _mockLogger.Verify(
            l => l.Error(
                It.Is<string>(s => s.Contains("error executing")),
                It.Is<Exception>(ex => ex == testException)),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task OnTimerCallback_ContinuesAfterException()
    {
        var executionCount = 0;
        var secondExecutionSignal = new TaskCompletionSource<bool>();
        var interval = TimeSpan.FromMilliseconds(100);

        _executor = new DroppingScheduledExecutor(
            async () =>
            {
                await Task.CompletedTask;
                executionCount++;
                if (executionCount == 1)
                {
                    throw new InvalidOperationException("First execution error");
                }
                else if (executionCount == 2)
                {
                    secondExecutionSignal.TrySetResult(true);
                }
            },
            interval,
            _mockLogger.Object);

        _executor.Start();

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(secondExecutionSignal.Task, timeoutTask);

        Assert.AreNotEqual(timeoutTask, completedTask, "Second execution did not complete within timeout");
        Assert.IsGreaterThanOrEqualTo(executionCount, 2, "Executor should continue after exception");
    }
}
