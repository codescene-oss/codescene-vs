using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models.Ace;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class PreflightManagerTests
{
    private Mock<ICliExecutor> _mockCliExecutor;
    private Mock<ILogger> _mockLogger;
    private Mock<IAceStateService> _mockAceStateService;
    private PreflightManager _preflightManager;

    [TestInitialize]
    public void Setup()
    {
        _mockCliExecutor = new Mock<ICliExecutor>();
        _mockLogger = new Mock<ILogger>();
        _mockAceStateService = new Mock<IAceStateService>();
        _preflightManager = new PreflightManager(_mockCliExecutor.Object, _mockLogger.Object, _mockAceStateService.Object);
    }

    [TestMethod]
    public void IsSupportedLanguage_WhenNoPreflightResponse_ReturnsFalse()
    {
        // No preflight has been run, so _preflightResponse is null
        var result = _preflightManager.IsSupportedLanguage(".cs");

        Assert.IsFalse(result);
    }

    [TestMethod]
    [Description("Extension normalization logic test - demonstrates the normalization logic")]
    public void IsSupportedLanguage_NormalizesExtension_RemovesDotAndConvertsToLowercase()
    {
        var resultWithDot = _preflightManager.IsSupportedLanguage(".CS");
        var resultWithoutDot = _preflightManager.IsSupportedLanguage("cs");
        var resultUpperCase = _preflightManager.IsSupportedLanguage("CS");

        // All return false because no preflight response
        Assert.IsFalse(resultWithDot);
        Assert.IsFalse(resultWithoutDot);
        Assert.IsFalse(resultUpperCase);
    }

    [TestMethod]
    public void RunPreflight_LogsDebugMessage()
    {
        // Act
        _preflightManager.RunPreflight(true);

        // Assert
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Running preflight"))), Times.Once);
    }

    [TestMethod]
    [Description("GetPreflightResponse calls RunPreflight when not cached")]
    public void GetPreflightResponse_WhenNotCached_CallsRunPreflight()
    {
        // Act
        var result = _preflightManager.GetPreflightResponse();

        // Assert
        Assert.IsNull(result);
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Running preflight"))), Times.Once);
    }

    [TestMethod]
    public void GetAutoRefactorConfig_WhenNotSet_ReturnsDefaultConfig()
    {
        // Act
        var config = _preflightManager.GetAutoRefactorConfig();

        // Assert
        Assert.IsTrue(config.Activated);
        Assert.IsTrue(config.Visible);
        Assert.IsFalse(config.Disabled);
    }

    [TestMethod]
    public void RunPreflight_WhenExecutorThrowsException_SetsErrorState()
    {
        // Arrange
        var expectedException = new Exception("CLI error");
        _mockCliExecutor.Setup(x => x.Preflight(It.IsAny<bool>()))
            .Throws(expectedException);

        // Act
        var result = _preflightManager.RunPreflight();

        // Assert
        Assert.IsNull(result);
        _mockAceStateService.Verify(s => s.SetState(AceState.Error, expectedException), Times.Once);
        _mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Problem getting preflight")), expectedException), Times.Once);
    }

    [TestMethod]
    public void RunPreflight_WhenExecutorReturnsNull_SetsOfflineState()
    {
        // Arrange
        _mockCliExecutor.Setup(x => x.Preflight(It.IsAny<bool>()))
            .Returns((PreFlightResponseModel)null);

        // Act
        var result = _preflightManager.RunPreflight();

        // Assert
        Assert.IsNull(result);
        _mockAceStateService.Verify(s => s.SetState(AceState.Offline), Times.Once);
        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("ACE service is down"))), Times.Once);
    }

    [TestMethod]
    public void RunPreflight_WhenExecutorReturnsResponse_SetsEnabledState()
    {
        // Arrange
        var response = new PreFlightResponseModel { FileTypes = new[] { "cs", "js" } };
        _mockCliExecutor.Setup(x => x.Preflight(It.IsAny<bool>()))
            .Returns(response);

        // Act
        var result = _preflightManager.RunPreflight();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(response, result);
        _mockAceStateService.Verify(s => s.SetState(AceState.Loading), Times.Once);
        _mockAceStateService.Verify(s => s.SetState(AceState.Enabled), Times.Once);
        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("ACE service is active"))), Times.Once);
    }

    [TestMethod]
    public void IsSupportedLanguage_WhenPreflightResponseExists_ReturnsTrue()
    {
        // Arrange
        var response = new PreFlightResponseModel { FileTypes = new[] { "cs", "js", "ts" } };
        _mockCliExecutor.Setup(x => x.Preflight(It.IsAny<bool>()))
            .Returns(response);
        _preflightManager.RunPreflight();

        // Act
        var result = _preflightManager.IsSupportedLanguage(".cs");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void GetPreflightResponse_WhenCached_ReturnsCachedResponse()
    {
        // Arrange
        var response = new PreFlightResponseModel { FileTypes = new[] { "cs" } };
        _mockCliExecutor.Setup(x => x.Preflight(It.IsAny<bool>()))
            .Returns(response);
        _preflightManager.RunPreflight();
        _mockCliExecutor.Invocations.Clear();

        // Act
        var result = _preflightManager.GetPreflightResponse();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(response, result);
        _mockCliExecutor.Verify(x => x.Preflight(It.IsAny<bool>()), Times.Never);
    }
}
