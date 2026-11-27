// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Security;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Options;

internal class OptionsProvider
{
    // Register the options with this attribute on your package class:
    // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene.VSExtension.VS2022", "General", 0, 0, true, SupportsProfiles = true)]
    [ComVisible(true)]
    public class GeneralOptions : BaseOptionPage<General>
    {
    }
}

public class General : BaseOptionModel<General>
{
    private byte[] _previousAuthToken;
    private bool _previousShowDebugLogs;
    private bool _previousEnableAutomaticAnalysis = true;
    private bool _previousBrowserSignIn;
    private string _previousServerUrl;
    private byte[] _previousPlainAuthToken;
    private bool _previousEnableRefactoringAgent;

    public General()
    {
        Load();

        _previousAuthToken = SecretBuffer.FromString(AuthToken);
        _previousShowDebugLogs = ShowDebugLogs;
        _previousEnableAutomaticAnalysis = EnableAutomaticAnalysis;
        _previousBrowserSignIn = BrowserSignIn;
        _previousServerUrl = ServerUrl;
        _previousPlainAuthToken = SecretBuffer.FromString(AuthToken);
        _previousEnableRefactoringAgent = EnableRefactoringAgent;

        Saved += OnSettingsSaved;
    }

    /// <summary>
    /// Event fired when the AuthToken setting changes.
    /// </summary>
    public static event EventHandler AuthTokenChanged;

    /// <summary>
    /// Event fired when the ShowDebugLogs setting changes.
    /// </summary>
    public static event EventHandler ShowDebugLogsChanged;

    /// <summary>
    /// Event fired when any setting is saved.
    /// </summary>
    public static event EventHandler SettingsSaved;

    public static event EventHandler AuthSettingsChanged;

    public static event EventHandler RefactoringAgentEnabledChanged;

    [Category("General")]
    [DisplayName("Show Debug Logs")]
    [Description("Enable detailed debug logs in the CodeScene Output window")]
    public bool ShowDebugLogs { get; set; } = false;

    [Category("Experimental")]
    [DisplayName("Browser sign-in")]
    [Description("Sign in to CodeScene in the browser (experimental). Requires a CodeScene IDE server with sign-in support.")]
    public bool BrowserSignIn { get; set; } = false;

    [Category("Authentication")]
    [DisplayName("Server URL")]
    [Description("CodeScene server for browser sign-in. Leave empty to use CS_ONPREM_URL, then CodeScene Cloud. Used when Browser sign-in (experimental) is on.")]
    public string ServerUrl { get; set; } = string.Empty;

    [Category("Authentication")]
    [DisplayName("Auth Token")]
    [Description("Personal access token — an alternative to signing in. Also used for refactoring. The token is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings.")]
    [PasswordPropertyText(true)]
    public string AuthToken { get; set; } = string.Empty;

    [Category("Performance")]
    [DisplayName("Server Worker Threads")]
    [Description("Number of worker threads for the CodeScene CLI server. Use 0 to choose automatically (half of the logical processors).")]
    public int ServerWorkerThreads { get; set; } = 0;

    [Category("General")]
    [DisplayName("Enable Telemetry")]
    [Description("Enable sending usage telemetry to CodeScene")]
    public bool EnableTelemetry { get; set; } = true;

    [Category("General")]
    [DisplayName("Enable Automatic Analysis")]
    [Description("Automatically analyze file changes in the workspace. Open editors are still reviewed for diagnostics when this is off.")]
    public bool EnableAutomaticAnalysis { get; set; } = true;

    [Category("Refactoring Agent")]
    [DisplayName("\t\t\t\t\t\t\tEnable Refactoring Agent")]
    [Description("Enable CodeScene Refactoring Agent")]
    public bool EnableRefactoringAgent { get; set; } = false;

    [Category("Refactoring Agent")]
    [DisplayName("\t\t\t\t\t\tAgent Model")]
    [Description("Model to use for the Refactoring Agent.")]
    [TypeConverter(typeof(AgentModelConverter))]
    public string AgentModel { get; set; } = AgentModels.Default;

    [Category("Refactoring Agent")]
    [DisplayName("\t\t\t\t\tAgent Model Override")]
    [Description("Custom model identifier. If set, overrides the dropdown selection above.")]
    public string AgentModelOverride { get; set; } = string.Empty;

    [Category("Refactoring Agent")]
    [DisplayName("\tAmazon Bedrock Profile")]
    [Description("AWS profile name for Amazon Bedrock. Check the setup with Extensions > CodeScene > Test provider connectivity.")]
    public string BedrockProfile { get; set; } = string.Empty;

    [Category("Refactoring Agent")]
    [DisplayName("Amazon Bedrock Region")]
    [Description("AWS region for Amazon Bedrock. Check the setup with Extensions > CodeScene > Test provider connectivity.")]
    public string BedrockRegion { get; set; } = string.Empty;

    [Category("Refactoring Agent")]
    [DisplayName("\t\t\t\tAnthropic API Key")]
    [Description("API key for Anthropic models. The key is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings.")]
    [PasswordPropertyText(true)]
    public string AnthropicApiKey { get; set; } = string.Empty;

    [Category("Refactoring Agent")]
    [DisplayName("\t\t\tOpenAI API Key")]
    [Description("API key for OpenAI models. The key is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings.")]
    [PasswordPropertyText(true)]
    public string OpenAIApiKey { get; set; } = string.Empty;

    [Category("Refactoring Agent")]
    [DisplayName("\t\tGoogle API Key")]
    [Description("API key for Google (Gemini) models. The key is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings.")]
    [PasswordPropertyText(true)]
    public string GoogleApiKey { get; set; } = string.Empty;

    public override async Task LoadAsync()
    {
        await base.LoadAsync();
        AuthToken = AuthTokenProtector.Unprotect(AuthToken);
        AnthropicApiKey = UnprotectProviderKey(AnthropicApiKey);
        OpenAIApiKey = UnprotectProviderKey(OpenAIApiKey);
        GoogleApiKey = UnprotectProviderKey(GoogleApiKey);
        RememberAuthSettings(AuthToken);
    }

    public override async Task SaveAsync()
    {
        var plaintext = AuthToken;
        var anthropicApiKey = AnthropicApiKey;
        var openAIApiKey = OpenAIApiKey;
        var googleApiKey = GoogleApiKey;
        AuthToken = AuthTokenProtector.Protect(plaintext);
        AnthropicApiKey = ProviderKeyProtector.Protect(anthropicApiKey?.Trim());
        OpenAIApiKey = ProviderKeyProtector.Protect(openAIApiKey?.Trim());
        GoogleApiKey = ProviderKeyProtector.Protect(googleApiKey?.Trim());
        try
        {
            await base.SaveAsync();
        }
        finally
        {
            AuthToken = plaintext;
            AnthropicApiKey = anthropicApiKey;
            OpenAIApiKey = openAIApiKey;
            GoogleApiKey = googleApiKey;
        }
    }

    private static string UnprotectProviderKey(string stored)
    {
        return ProviderKeyProtector.TryUnprotect(stored, out var plaintext) ? plaintext : string.Empty;
    }

    private void OnSettingsSaved(General obj)
    {
        var package = VS2022Package.Instance;
        package?.JoinableTaskFactory.RunAsync(() => VS.StatusBar.ShowMessageAsync("Options Saved")).FileAndForget("General/OptionsSaved");

        // Fire general settings saved event
        SettingsSaved?.Invoke(this, EventArgs.Empty);

        // Check for specific setting changes and fire targeted events
        if (!SecretBuffer.Equals(_previousAuthToken, AuthToken))
        {
            SecretBuffer.Replace(ref _previousAuthToken, AuthToken);
            AuthTokenChanged?.Invoke(this, EventArgs.Empty);
        }

        DetectAuthSettingsChange();

        if (_previousShowDebugLogs != ShowDebugLogs)
        {
            _previousShowDebugLogs = ShowDebugLogs;
            ShowDebugLogsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_previousEnableAutomaticAnalysis != EnableAutomaticAnalysis)
        {
            _previousEnableAutomaticAnalysis = EnableAutomaticAnalysis;
            ApplyAutomaticAnalysis(EnableAutomaticAnalysis);
        }

        if (_previousEnableRefactoringAgent != EnableRefactoringAgent)
        {
            _previousEnableRefactoringAgent = EnableRefactoringAgent;
            RefactoringAgentEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DetectAuthSettingsChange()
    {
        var plaintext = AuthTokenProtector.Unprotect(AuthToken);
        var changes = AuthSettingsComparison.Detect(
            _previousBrowserSignIn,
            BrowserSignIn,
            _previousServerUrl,
            ServerUrl,
            _previousPlainAuthToken,
            plaintext);

        if (!changes.AuthSettingsChanged)
        {
            return;
        }

        RememberAuthSettings(plaintext);
        AuthSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RememberAuthSettings(string plaintextToken)
    {
        _previousBrowserSignIn = BrowserSignIn;
        _previousServerUrl = ServerUrl;
        SecretBuffer.Replace(ref _previousPlainAuthToken, plaintextToken);
    }

    private void ApplyAutomaticAnalysis(bool enabled)
    {
        var package = VS2022Package.Instance;
        package?.JoinableTaskFactory.RunAsync(async () =>
        {
            var coordinator = await VS.GetMefServiceAsync<IWorkspaceWatchCoordinator>();
            coordinator?.SetEnabled(enabled);
            var notifier = await VS.GetMefServiceAsync<ICodeHealthMonitorNotifier>();
            notifier?.SetAutomaticAnalysisEnabled(enabled);
            if (!enabled)
            {
                new DeltaCacheService().Clear();
                notifier?.RequestViewUpdate();
            }
        }).FileAndForget("General/AutomaticAnalysis");
    }
}
