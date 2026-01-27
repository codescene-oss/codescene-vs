using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class DebounceServiceTests
{
    private static readonly TimeSpan SafetyBuffer = TimeSpan.FromMilliseconds(50);
    
    private Mock<ILogger> _mockLogger;
    private DebounceService _debounceService;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _debounceService = new DebounceService(_mockLogger.Object);
    }
    
    private static Task WaitForDebounce(TimeSpan delay) => Task.Delay(delay + SafetyBuffer);
    
    private sealed class ActionTracker
    {
        public bool WasExecuted { get; private set; }
        public Action Execute => () => WasExecuted = true;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _debounceService.Dispose();
    }

    [TestMethod]
    public async Task Debounce_ExecutesActionAfterDelay()
    {
        // Arrange
        var actionExecutedSignal = new TaskCompletionSource<bool>();
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        _debounceService.Debounce("test-key", () => actionExecutedSignal.TrySetResult(true), delay);

        // Assert
        Assert.IsFalse(actionExecutedSignal.Task.IsCompleted);

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(actionExecutedSignal.Task, timeoutTask);

        Assert.AreNotEqual(timeoutTask, completedTask, "Action was not executed within timeout");
        Assert.IsTrue(await actionExecutedSignal.Task);
    }

    [TestMethod]
    public async Task Debounce_CancelsPreviousAction_WhenCalledAgain()
    {
        // Arrange
        var firstActionSignal = new TaskCompletionSource<bool>();
        var secondActionSignal = new TaskCompletionSource<bool>();
        var delay = TimeSpan.FromMilliseconds(200);

        // Act - debounce same key twice, first should be cancelled
        _debounceService.Debounce("test-key", () => firstActionSignal.TrySetResult(true), delay);
        await Task.Delay(50);
        _debounceService.Debounce("test-key", () => secondActionSignal.TrySetResult(true), delay);

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(secondActionSignal.Task, timeoutTask);

        // Assert
        Assert.IsFalse(firstActionSignal.Task.IsCompleted, "First action should have been cancelled");
        Assert.AreNotEqual(timeoutTask, completedTask, "Second action was not executed within timeout");
        Assert.IsTrue(await secondActionSignal.Task, "Second action should have executed");
    }

    [TestMethod]
    public async Task Debounce_DifferentKeys_ExecuteBothActions()
    {
        // Arrange
        var signal1 = new TaskCompletionSource<bool>();
        var signal2 = new TaskCompletionSource<bool>();

        // Act - debounce with different keys simultaneously
        _debounceService.Debounce("key1", () => signal1.TrySetResult(true), TimeSpan.FromMilliseconds(100));
        _debounceService.Debounce("key2", () => signal2.TrySetResult(true), TimeSpan.FromMilliseconds(100));

        var bothActionsTask = Task.WhenAll(signal1.Task, signal2.Task);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(bothActionsTask, timeoutTask);

        // Assert - both should execute since keys are different
        Assert.AreNotEqual(timeoutTask, completedTask, "Actions were not executed within timeout");
        Assert.IsTrue(await signal1.Task, "Action for key1 should have executed");
        Assert.IsTrue(await signal2.Task, "Action for key2 should have executed");
    }

    [TestMethod]
    public async Task Debounce_ActionNotExecuted_WhenDisposedBeforeDelay()
    {
        // Arrange
        var tracker = new ActionTracker();
        var delay = TimeSpan.FromMilliseconds(200);

        // Act
        _debounceService.Debounce("test-key", tracker.Execute, delay);
        _debounceService.Dispose();
        await WaitForDebounce(delay);

        // Assert
        Assert.IsFalse(tracker.WasExecuted, "Action should not execute after dispose");
    }

    [TestMethod]
    public async Task Debounce_LogsDebugMessage_WhenActionExecutes()
    {
        // Arrange
        var actionExecutedSignal = new TaskCompletionSource<bool>();
        var delay = TimeSpan.FromMilliseconds(50);

        // Act
        _debounceService.Debounce("test-key", () => actionExecutedSignal.TrySetResult(true), delay);

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(actionExecutedSignal.Task, timeoutTask);

        // Assert
        Assert.AreNotEqual(timeoutTask, completedTask, "Action was not executed within timeout");
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("test-key"))), Times.Once);
    }

    [TestMethod]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act & Assert - should not throw
        _debounceService.Dispose();
        _debounceService.Dispose();
    }
}
