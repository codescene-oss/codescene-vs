// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class PreflightManagerTests
{
    private Mock<ICliExecutor> _mockCliExecutor;
    private Mock<ILogger> _mockLogger;
    private Mock<IAceStateService> _mockAceStateService;
    private Mock<ISettingsProvider> _mockSettingsProvider;
    private PreflightManager _preflightManager;

    [TestInitialize]
    public void Setup()
    {
        _mockCliExecutor = new Mock<ICliExecutor>();
        _mockLogger = new Mock<ILogger>();
        _mockAceStateService = new Mock<IAceStateService>();
        _mockSettingsProvider = new Mock<ISettingsProvider>();
        _preflightManager = new PreflightManager(_mockCliExecutor.Object, _mockLogger.Object, _mockAceStateService.Object, _mockSettingsProvider.Object);
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
    public async Task RunPreflightAsync_LogsDebugMessage()
    {
        // Act
        await _preflightManager.RunPreflightAsync(true);

        // Assert
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Running preflight"))), Times.Once);
    }

    [TestMethod]
    [Description("GetPreflightResponse calls RunPreflight when not cached")]
    public async Task GetPreflightResponseAsync_WhenNotCached_CallsRunPreflight()
    {
        // Act
        var result = await _preflightManager.GetPreflightResponseAsync();

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
        Assert.IsNotNull(config.AceStatus);
        Assert.AreEqual("disabled", config.AceStatus.Status);
        Assert.IsFalse(config.AceStatus.HasToken);
    }

    [TestMethod]
    public async Task RunPreflightAsync_WhenExecutorThrowsException_SetsErrorState()
    {
        // Arrange
        var expectedException = new Exception("CLI error");
        SetupPreflightThrows(expectedException);

        // Act
        var result = await _preflightManager.RunPreflightAsync();

        // Assert
        Assert.IsNull(result);
        _mockAceStateService.Verify(s => s.SetState(AceState.Error, expectedException), Times.Once);
        _mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Problem getting preflight")), expectedException), Times.Once);
    }

    [TestMethod]
    public async Task RunPreflightAsync_WhenExecutorReturnsNull_SetsOfflineState()
    {
        // Arrange
        SetupPreflightReturnsNull();

        // Act
        var result = await _preflightManager.RunPreflightAsync();

        // Assert
        Assert.IsNull(result);
        _mockAceStateService.Verify(s => s.SetState(AceState.Offline), Times.Once);
        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("ACE service is down"))), Times.Once);
    }

    [TestMethod]
    public async Task RunPreflightAsync_WhenExecutorReturnsResponse_SetsEnabledState()
    {
        // Arrange
        var response = CreatePreflightResponse("cs", "js");
        SetupSuccessfulPreflight(response);

        // Act
        var result = await _preflightManager.RunPreflightAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(response, result);
        _mockAceStateService.Verify(s => s.SetState(AceState.Loading), Times.Once);
        _mockAceStateService.Verify(s => s.SetState(AceState.Enabled), Times.Once);
        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("ACE service is active"))), Times.Once);
    }

    [TestMethod]
    public async Task IsSupportedLanguage_WhenPreflightResponseExists_ReturnsTrue()
    {
        // Arrange
        SetupSuccessfulPreflight(CreatePreflightResponse("cs", "js", "ts"));
        await _preflightManager.RunPreflightAsync();

        // Act & Assert
        Assert.IsTrue(_preflightManager.IsSupportedLanguage(".cs"));
    }

    [TestMethod]
    public async Task GetPreflightResponseAsync_WhenCached_ReturnsCachedResponse()
    {
        // Arrange
        var response = CreatePreflightResponse("cs");
        SetupSuccessfulPreflight(response);
        await _preflightManager.RunPreflightAsync();
        _mockCliExecutor.Invocations.Clear();

        // Act
        var result = await _preflightManager.GetPreflightResponseAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(response, result);
        _mockCliExecutor.Verify(x => x.PreflightAsync(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task RunPreflightAsync_WhenSuccessful_SetsAceStatusWithToken()
    {
        // Arrange
        SetupPreflightWithAceStatus("valid-token", AceState.Enabled);

        // Act & Assert
        await RunPreflightAndAssertAceStatus("enabled", expectedHasToken: true);
    }

    [TestMethod]
    public async Task RunPreflightAsync_WhenSuccessful_SetsAceStatusWithoutToken()
    {
        // Arrange
        SetupPreflightWithAceStatus(string.Empty, AceState.Enabled);

        // Act & Assert
        await RunPreflightAndAssertAceStatus("enabled", expectedHasToken: false);
    }

    [TestMethod]
    public async Task RunPreflightAsync_WhenOffline_SetsCorrectAceStatus()
    {
        // Arrange
        SetupPreflightReturnsNull();
        SetupAuthToken("token");
        SetupCurrentState(AceState.Loading);

        // Act & Assert
        await RunPreflightAndAssertAceStatus("loading", expectedHasToken: true);
    }

    [TestMethod]
    public async Task RunPreflightAsync_WhenError_SetsCorrectAceStatus()
    {
        // Arrange
        SetupPreflightThrows(new Exception("Test error"));
        SetupAuthToken("token");
        SetupCurrentState(AceState.Error);

        // Act & Assert
        await RunPreflightAndAssertAceStatus("error", expectedHasToken: true);
    }

    [TestMethod]
    public async Task SetHasAceToken_UpdatesHasTokenCorrectly()
    {
        // Arrange - initialize config without token
        await InitializePreflightConfig(token: string.Empty);
        Assert.IsFalse(_preflightManager.GetAutoRefactorConfig().AceStatus.HasToken);

        // Act
        _preflightManager.SetHasAceToken(true);

        // Assert
        Assert.IsTrue(_preflightManager.GetAutoRefactorConfig().AceStatus.HasToken);
    }

    [TestMethod]
    public async Task SetHasAceToken_CanSetToFalse()
    {
        // Arrange - initialize config with token
        await InitializePreflightConfig(token: "valid-token");
        Assert.IsTrue(_preflightManager.GetAutoRefactorConfig().AceStatus.HasToken);

        // Act
        _preflightManager.SetHasAceToken(false);

        // Assert
        Assert.IsFalse(_preflightManager.GetAutoRefactorConfig().AceStatus.HasToken);
    }

    [TestMethod]
    public void SetHasAceToken_WhenConfigNotInitialized_DoesNotThrow()
    {
        // Arrange - don't run preflight, so _autoRefactorConfig is null

        // Act & Assert - should not throw, just return early
        _preflightManager.SetHasAceToken(true);

        // Verify config still returns default (unchanged)
        var config = _preflightManager.GetAutoRefactorConfig();
        Assert.IsFalse(config.AceStatus.HasToken);
    }

    [TestMethod]
    public async Task IsSupportedLanguage_WhenExtensionNotInList_ReturnsFalse()
    {
        // Arrange
        SetupSuccessfulPreflight(CreatePreflightResponse("cs", "js"));
        await _preflightManager.RunPreflightAsync();

        // Act
        var result = _preflightManager.IsSupportedLanguage(".py");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsSupportedLanguage_WithUpperCaseExtension_ReturnsTrue()
    {
        // Arrange
        SetupSuccessfulPreflight(CreatePreflightResponse("cs", "js"));
        await _preflightManager.RunPreflightAsync();

        // Act
        var result = _preflightManager.IsSupportedLanguage(".CS");

        // Assert - extension is normalized to lowercase
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsSupportedLanguage_WithoutDotPrefix_ReturnsTrue()
    {
        // Arrange
        SetupSuccessfulPreflight(CreatePreflightResponse("cs", "js"));
        await _preflightManager.RunPreflightAsync();

        // Act
        var result = _preflightManager.IsSupportedLanguage("cs");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow(null, false, DisplayName = "Null token sets HasToken to false")]
    [DataRow("", false, DisplayName = "Empty token sets HasToken to false")]
    [DataRow("   ", false, DisplayName = "Whitespace token sets HasToken to false")]
    [DataRow("valid-token", true, DisplayName = "Valid token sets HasToken to true")]
    public async Task RunPreflightAsync_SetsHasTokenBasedOnAuthToken(string token, bool expectedHasToken)
    {
        // Arrange
        SetupSuccessfulPreflight();
        SetupAuthToken(token);
        SetupCurrentState(AceState.Enabled);

        // Act
        await _preflightManager.RunPreflightAsync();
        var config = _preflightManager.GetAutoRefactorConfig();

        // Assert
        Assert.AreEqual(expectedHasToken, config.AceStatus.HasToken);
    }

    [TestMethod]
    public async Task GetAutoRefactorConfig_AfterSuccessfulPreflight_ReturnsConfiguredValues()
    {
        // Arrange
        SetupPreflightWithAceStatus("token", AceState.Enabled);
        await _preflightManager.RunPreflightAsync();

        // Act
        var config = _preflightManager.GetAutoRefactorConfig();

        // Assert
        Assert.IsTrue(config.Activated);
        Assert.IsTrue(config.Visible);
        Assert.IsFalse(config.Disabled);
        Assert.IsNotNull(config.AceStatus);
    }

    [TestMethod]
    [DataRow(null, true, DisplayName = "Null token sets Disabled to true")]
    [DataRow("", true, DisplayName = "Empty token sets Disabled to true")]
    [DataRow("   ", true, DisplayName = "Whitespace token sets Disabled to true")]
    [DataRow("valid-token", false, DisplayName = "Valid token sets Disabled to false")]
    public async Task RunPreflightAsync_SetsDisabledBasedOnAuthToken(string token, bool expectedDisabled)
    {
        // Arrange
        SetupPreflightWithAceStatus(token, AceState.Enabled);

        // Act
        await _preflightManager.RunPreflightAsync();
        var config = _preflightManager.GetAutoRefactorConfig();

        // Assert
        Assert.AreEqual(expectedDisabled, config.Disabled);
    }

    [TestMethod]
    public async Task SetHasAceToken_WhenSetToTrue_SetsDisabledFalse()
    {
        // Arrange - initialize config without token (Disabled = true)
        await InitializePreflightConfig(token: string.Empty);
        Assert.IsTrue(_preflightManager.GetAutoRefactorConfig().Disabled);

        // Act
        _preflightManager.SetHasAceToken(true);

        // Assert
        Assert.IsFalse(_preflightManager.GetAutoRefactorConfig().Disabled);
    }

    [TestMethod]
    public async Task SetHasAceToken_WhenSetToFalse_SetsDisabledTrue()
    {
        // Arrange - initialize config with valid token (Disabled = false)
        await InitializePreflightConfig(token: "valid-token");
        Assert.IsFalse(_preflightManager.GetAutoRefactorConfig().Disabled);

        // Act
        _preflightManager.SetHasAceToken(false);

        // Assert
        Assert.IsTrue(_preflightManager.GetAutoRefactorConfig().Disabled);
    }

    private PreFlightResponseModel CreatePreflightResponse(params string[] fileTypes) =>
    new PreFlightResponseModel { FileTypes = fileTypes };

    private void SetupSuccessfulPreflight(PreFlightResponseModel? response = null)
    {
        response ??= CreatePreflightResponse("cs");
        _mockCliExecutor.Setup(x => x.PreflightAsync(It.IsAny<bool>())).ReturnsAsync(response);
    }

    private void SetupPreflightReturnsNull() =>
        _mockCliExecutor.Setup(x => x.PreflightAsync(It.IsAny<bool>())).ReturnsAsync((PreFlightResponseModel)null);

    private void SetupPreflightThrows(Exception exception) =>
        _mockCliExecutor.Setup(x => x.PreflightAsync(It.IsAny<bool>())).ThrowsAsync(exception);

    private void SetupAuthToken(string token) =>
        _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);

    private void SetupCurrentState(AceState state) =>
        _mockAceStateService.Setup(x => x.CurrentState).Returns(state);

    private void SetupPreflightWithAceStatus(string token, AceState state, PreFlightResponseModel? response = null)
    {
        SetupSuccessfulPreflight(response);
        SetupAuthToken(token);
        SetupCurrentState(state);
    }

    private async Task RunPreflightAndAssertAceStatus(string expectedStatus, bool expectedHasToken)
    {
        await _preflightManager.RunPreflightAsync();
        var config = _preflightManager.GetAutoRefactorConfig();

        Assert.IsNotNull(config.AceStatus);
        Assert.AreEqual(expectedStatus, config.AceStatus.Status);
        Assert.AreEqual(expectedHasToken, config.AceStatus.HasToken);
    }

    private async Task InitializePreflightConfig(string token = "token")
    {
        SetupPreflightWithAceStatus(token, AceState.Enabled);
        await _preflightManager.RunPreflightAsync();
    }
}
