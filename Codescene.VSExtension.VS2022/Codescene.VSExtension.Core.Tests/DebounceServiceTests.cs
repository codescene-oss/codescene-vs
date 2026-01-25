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
        var firstAction = new ActionTracker();
        var secondAction = new ActionTracker();
        var delay = TimeSpan.FromMilliseconds(200);

        // Act - debounce same key twice, first should be cancelled
        _debounceService.Debounce("test-key", firstAction.Execute, delay);
        await Task.Delay(50);
        _debounceService.Debounce("test-key", secondAction.Execute, delay);
        await WaitForDebounce(delay);

        // Assert
        Assert.IsFalse(firstAction.WasExecuted, "First action should have been cancelled");
        Assert.IsTrue(secondAction.WasExecuted, "Second action should have executed");
    }

    [TestMethod]
    public async Task Debounce_DifferentKeys_ExecuteBothActions()
    {
        // Arrange
        var tracker1 = new ActionTracker();
        var tracker2 = new ActionTracker();

        // Act - debounce with different keys simultaneously
        _debounceService.Debounce("key1", tracker1.Execute, TimeSpan.FromMilliseconds(100));
        _debounceService.Debounce("key2", tracker2.Execute, TimeSpan.FromMilliseconds(100));
        await WaitForDebounce(TimeSpan.FromMilliseconds(100));

        // Assert - both should execute since keys are different
        Assert.IsTrue(tracker1.WasExecuted, "Action for key1 should have executed");
        Assert.IsTrue(tracker2.WasExecuted, "Action for key2 should have executed");
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
        var delay = TimeSpan.FromMilliseconds(50);

        // Act
        _debounceService.Debounce("test-key", () => { }, delay);
        await WaitForDebounce(delay);

        // Assert
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
