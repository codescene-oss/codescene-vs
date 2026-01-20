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
        var actionExecuted = false;
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        _debounceService.Debounce("test-key", () => actionExecuted = true, delay);

        // Assert - action should not be executed immediately
        Assert.IsFalse(actionExecuted);

        // Wait for debounce to complete
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Now action should be executed
        Assert.IsTrue(actionExecuted);
    }

    [TestMethod]
    public async Task Debounce_CancelsPreviousAction_WhenCalledAgain()
    {
        // Arrange
        var firstActionExecuted = false;
        var secondActionExecuted = false;
        var delay = TimeSpan.FromMilliseconds(200);

        // Act - call debounce twice with same key
        _debounceService.Debounce("test-key", () => firstActionExecuted = true, delay);
        await Task.Delay(50); // Wait a bit before second call
        _debounceService.Debounce("test-key", () => secondActionExecuted = true, delay);

        // Wait for debounce to complete
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert - first action should be cancelled, second should execute
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

        // Wait for debounce to complete
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert - both actions should execute (different keys)
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

        // Wait for what would have been the debounce completion
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert - action should not be executed because service was disposed
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
