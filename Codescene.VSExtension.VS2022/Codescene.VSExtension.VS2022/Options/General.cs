using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Codescene.VSExtension.VS2022;

internal partial class OptionsProvider
{
    // Register the options with this attribute on your package class:
    // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene.VSExtension.VS2022", "General", 0, 0, true, SupportsProfiles = true)]
    [ComVisible(true)]
    public class GeneralOptions : BaseOptionPage<General> { }
}

public class General : BaseOptionModel<General>
{
    // Track previous values to detect changes
    private string _previousAuthToken = string.Empty;
    private bool _previousShowDebugLogs = false;

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

    [Category("General")]
    [DisplayName("Show Debug Logs")]
    [Description("Enable detailed debug logs in the CodeScene Output window")]
    public bool ShowDebugLogs { get; set; } = false;

    [Category("Authentication")]
    [DisplayName("Auth Token")]
    [Description("Authentication token for CodeScene ACE. Note: Token is stored securely in Windows Credential Manager.")]
    [PasswordPropertyText(true)]
    public string AuthToken { get; set; } = string.Empty;

    public General()
        : base()
    {
        // Store initial values
        _previousAuthToken = AuthToken;
        _previousShowDebugLogs = ShowDebugLogs;

        Saved += OnSettingsSaved;
    }

    private void OnSettingsSaved(General obj)
    {
        VS.StatusBar.ShowMessageAsync("Options Saved").FireAndForget();

        // Fire general settings saved event
        SettingsSaved?.Invoke(this, EventArgs.Empty);

        // Check for specific setting changes and fire targeted events
        if (_previousAuthToken != AuthToken)
        {
            _previousAuthToken = AuthToken;
            AuthTokenChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_previousShowDebugLogs != ShowDebugLogs)
        {
            _previousShowDebugLogs = ShowDebugLogs;
            ShowDebugLogsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
