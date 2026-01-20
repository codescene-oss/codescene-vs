using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class DebounceServiceTests
{
    private Mock<ILogger> _mockLogger;
    private DebounceService _debounceService;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _debounceService = new DebounceService(_mockLogger.Object);
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
        var firstActionExecuted = false;
        var secondActionExecuted = false;
        var delay = TimeSpan.FromMilliseconds(200);

        // Act 
        _debounceService.Debounce("test-key", () => firstActionExecuted = true, delay);
        await Task.Delay(50);
        _debounceService.Debounce("test-key", () => secondActionExecuted = true, delay);

        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.IsFalse(firstActionExecuted);
        Assert.IsTrue(secondActionExecuted);
    }

    [TestMethod]
    public async Task Debounce_DifferentKeys_ExecuteBothActions()
    {
        // Arrange
        var action1Executed = false;
        var action2Executed = false;
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        _debounceService.Debounce("key1", () => action1Executed = true, delay);
        _debounceService.Debounce("key2", () => action2Executed = true, delay);

        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.IsTrue(action1Executed);
        Assert.IsTrue(action2Executed);
    }

    [TestMethod]
    public async Task Debounce_ActionNotExecuted_WhenDisposedBeforeDelay()
    {
        // Arrange
        var actionExecuted = false;
        var delay = TimeSpan.FromMilliseconds(200);

        // Act
        _debounceService.Debounce("test-key", () => actionExecuted = true, delay);
        _debounceService.Dispose();

        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.IsFalse(actionExecuted);
    }

    [TestMethod]
    public async Task Debounce_LogsDebugMessage_WhenActionExecutes()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(50);

        // Act
        _debounceService.Debounce("test-key", () => { }, delay);
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

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
