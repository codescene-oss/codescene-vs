using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Ace;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class AceStateServiceTests
{
    private Mock<ILogger> _mockLogger;
    private AceStateService _aceStateService;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _aceStateService = new AceStateService(_mockLogger.Object);
    }

    [TestMethod]
    public void InitialState_ShouldBeLoading()
    {
        Assert.AreEqual(AceState.Loading, _aceStateService.CurrentState);
    }

    [TestMethod]
    public void InitialLastError_ShouldBeNull()
    {
        Assert.IsNull(_aceStateService.LastError);
    }

    [TestMethod]
    public void SetState_ShouldUpdateCurrentState()
    {
        _aceStateService.SetState(AceState.Enabled);

        Assert.AreEqual(AceState.Enabled, _aceStateService.CurrentState);
    }

    [TestMethod]
    public void SetState_ShouldFireStateChangedEvent()
    {
        AceStateChangedEventArgs capturedArgs = null;
        _aceStateService.StateChanged += (s, e) => capturedArgs = e;

        _aceStateService.SetState(AceState.Enabled);

        Assert.IsNotNull(capturedArgs);
        Assert.AreEqual(AceState.Loading, capturedArgs.PreviousState);
        Assert.AreEqual(AceState.Enabled, capturedArgs.NewState);
    }

    [TestMethod]
    public void SetState_WithError_ShouldStoreError()
    {
        var exception = new Exception("Test error");

        _aceStateService.SetState(AceState.Error, exception);

        Assert.AreEqual(exception, _aceStateService.LastError);
    }

    [TestMethod]
    public void SetState_WithError_ShouldIncludeErrorInEventArgs()
    {
        AceStateChangedEventArgs capturedArgs = null;
        _aceStateService.StateChanged += (s, e) => capturedArgs = e;
        var exception = new Exception("Test error");

        _aceStateService.SetState(AceState.Error, exception);

        Assert.AreEqual(exception, capturedArgs.Error);
    }

    [TestMethod]
    public void SetError_ShouldStoreError()
    {
        var exception = new Exception("Test error");

        _aceStateService.SetError(exception);

        Assert.AreEqual(exception, _aceStateService.LastError);
    }

    [TestMethod]
    public void SetError_ShouldTransitionToErrorState()
    {
        _aceStateService.SetState(AceState.Enabled);
        var exception = new Exception("Test error");

        _aceStateService.SetError(exception);

        Assert.AreEqual(AceState.Error, _aceStateService.CurrentState);
    }

    [TestMethod]
    public void SetError_WhenAlreadyInErrorState_ShouldNotChangeState()
    {
        _aceStateService.SetState(AceState.Error, new Exception("First error"));
        var secondException = new Exception("Second error");

        _aceStateService.SetError(secondException);

        Assert.AreEqual(AceState.Error, _aceStateService.CurrentState);
        Assert.AreEqual(secondException, _aceStateService.LastError);
    }

    [TestMethod]
    public void SetError_WhenAlreadyInErrorState_ShouldStillFireEvent()
    {
        _aceStateService.SetState(AceState.Error, new Exception("First error"));
        var secondException = new Exception("Second error");
        AceStateChangedEventArgs capturedArgs = null;
        _aceStateService.StateChanged += (s, e) => capturedArgs = e;

        _aceStateService.SetError(secondException);

        Assert.IsNotNull(capturedArgs);
        Assert.AreEqual(AceState.Error, capturedArgs.PreviousState);
        Assert.AreEqual(AceState.Error, capturedArgs.NewState);
        Assert.AreEqual(secondException, capturedArgs.Error);
    }

    [TestMethod]
    public void ClearError_ShouldClearLastError()
    {
        _aceStateService.SetError(new Exception("Test error"));

        _aceStateService.ClearError();

        Assert.IsNull(_aceStateService.LastError);
    }

    [TestMethod]
    public void ClearError_ShouldNotChangeState()
    {
        _aceStateService.SetState(AceState.Enabled);
        _aceStateService.SetError(new Exception("Test error"));
        var stateBeforeClear = _aceStateService.CurrentState;

        _aceStateService.ClearError();

        Assert.AreEqual(stateBeforeClear, _aceStateService.CurrentState);
    }

    [TestMethod]
    public void SetState_ShouldLogDebugMessage()
    {
        _aceStateService.SetState(AceState.Enabled);

        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("ACE state changed"))), Times.Once);
    }

    [TestMethod]
    public void SetState_ToOffline_ShouldLogWarning()
    {
        _aceStateService.SetState(AceState.Enabled);

        _aceStateService.SetState(AceState.Offline);

        _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("offline mode"))), Times.Once);
    }

    [TestMethod]
    public void SetState_FromOfflineToEnabled_ShouldLogInfo()
    {
        _aceStateService.SetState(AceState.Offline);

        _aceStateService.SetState(AceState.Enabled);

        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("back online"))), Times.Once);
    }

    [TestMethod]
    public void SetState_ToEnabled_ShouldLogInfo()
    {
        // Initial state is Loading, transitioning to Enabled
        _aceStateService.SetState(AceState.Enabled);

        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("ACE is active"))), Times.Once);
    }

    [TestMethod]
    public void SetState_ToDisabled_ShouldLogInfo()
    {
        _aceStateService.SetState(AceState.Enabled);

        _aceStateService.SetState(AceState.Disabled);

        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("disabled"))), Times.Once);
    }

    [TestMethod]
    public void SetState_MultipleTransitions_ShouldTrackCorrectly()
    {
        var stateHistory = new List<AceState>();
        _aceStateService.StateChanged += (s, e) => stateHistory.Add(e.NewState);

        _aceStateService.SetState(AceState.Enabled);
        _aceStateService.SetState(AceState.Offline);
        _aceStateService.SetState(AceState.Enabled);
        _aceStateService.SetState(AceState.Error, new Exception("Test"));
        _aceStateService.SetState(AceState.Disabled);

        CollectionAssert.AreEqual(
            new[] { AceState.Enabled, AceState.Offline, AceState.Enabled, AceState.Error, AceState.Disabled },
            stateHistory);
    }
}
