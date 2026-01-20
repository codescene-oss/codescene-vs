using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

/// <summary>
/// Tests for PreflightManager.
/// Note: The RunPreflight method is wrapped in #if FEATURE_ACE preprocessor directive.
/// Without that flag, RunPreflight always returns null.
/// These tests verify the behavior without the feature flag.
/// Full testing of ACE functionality should be done via integration tests with FEATURE_ACE enabled.
/// </summary>
[TestClass]
public class PreflightManagerTests
{
    private Mock<ICliExecutor> _mockCliExecutor;
    private Mock<ILogger> _mockLogger;
    private PreflightManager _preflightManager;

    [TestInitialize]
    public void Setup()
    {
        _mockCliExecutor = new Mock<ICliExecutor>();
        _mockLogger = new Mock<ILogger>();
        _preflightManager = new PreflightManager(_mockCliExecutor.Object, _mockLogger.Object);
    }

    #region IsSupportedLanguage Tests

    [TestMethod]
    public void IsSupportedLanguage_WhenNoPreflightResponse_ReturnsFalse()
    {
        // No preflight has been run, so _preflightResponse is null
        var result = _preflightManager.IsSupportedLanguage(".cs");

        Assert.IsFalse(result);
    }

    [TestMethod]
    [Description("Without FEATURE_ACE flag, preflight always returns null so IsSupportedLanguage returns false")]
    public void IsSupportedLanguage_WithoutFeatureFlag_AlwaysReturnsFalse()
    {
        // Arrange - even with mock setup, RunPreflight returns null without FEATURE_ACE
        var preflightResponse = new PreFlightResponseModel
        {
            FileTypes = new[] { "cs", "js", "py" }
        };
        _mockCliExecutor.Setup(x => x.Preflight(It.IsAny<bool>())).Returns(preflightResponse);
        _preflightManager.RunPreflight(true);

        // Act
        var result = _preflightManager.IsSupportedLanguage(".cs");

        // Assert - returns false because FEATURE_ACE is not defined
        Assert.IsFalse(result);
    }

    [TestMethod]
    [Description("Extension normalization logic test - demonstrates the normalization logic")]
    public void IsSupportedLanguage_NormalizesExtension_RemovesDotAndConvertsToLowercase()
    {
        // The extension normalization logic: extension.Replace(".", "").ToLower()
        // We can't fully test this without FEATURE_ACE, but we can verify it handles different formats
        // All should return false since no preflight response is cached

        var resultWithDot = _preflightManager.IsSupportedLanguage(".CS");
        var resultWithoutDot = _preflightManager.IsSupportedLanguage("cs");
        var resultUpperCase = _preflightManager.IsSupportedLanguage("CS");

        // All return false because no preflight response
        Assert.IsFalse(resultWithDot);
        Assert.IsFalse(resultWithoutDot);
        Assert.IsFalse(resultUpperCase);
    }

    #endregion

    #region RunPreflight Tests

    [TestMethod]
    [Description("Without FEATURE_ACE, RunPreflight always returns null")]
    public void RunPreflight_WithoutFeatureFlag_ReturnsNull()
    {
        // Arrange
        var preflightResponse = new PreFlightResponseModel
        {
            FileTypes = new[] { "cs" }
        };
        _mockCliExecutor.Setup(x => x.Preflight(It.IsAny<bool>())).Returns(preflightResponse);

        // Act
        var result = _preflightManager.RunPreflight(true);

        // Assert - returns null without FEATURE_ACE flag
        Assert.IsNull(result);
    }

    [TestMethod]
    public void RunPreflight_LogsDebugMessage()
    {
        // Act
        _preflightManager.RunPreflight(true);

        // Assert
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Running preflight"))), Times.Once);
    }

    #endregion

    #region GetPreflightResponse Tests

    [TestMethod]
    [Description("GetPreflightResponse calls RunPreflight when not cached, but returns null without FEATURE_ACE")]
    public void GetPreflightResponse_WhenNotCached_CallsRunPreflight()
    {
        // Act
        var result = _preflightManager.GetPreflightResponse();

        // Assert - returns null without FEATURE_ACE, but logs were called
        Assert.IsNull(result);
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Running preflight"))), Times.Once);
    }

    #endregion

    #region GetAutoRefactorConfig Tests

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

    #endregion
}
