using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
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
    [Category("General")]
    [DisplayName("Show Debug Logs")]
    [Description("Enable detailed debug logs in the CodeScene Output window")]
    public bool ShowDebugLogs { get; set; } = false;

    [Category("Authentication")]
    [DisplayName("Auth Token")]
    [Description("Authentication token for CodeScene ACE. Note: Token is stored securely in Windows Credential Manager.")]
    [PasswordPropertyText(true)]
    public string AuthToken { get; set; } = string.Empty;

    public General() : base()
    {
        Saved += delegate { VS.StatusBar.ShowMessageAsync("Options Saved").FireAndForget(); };
    }
}
