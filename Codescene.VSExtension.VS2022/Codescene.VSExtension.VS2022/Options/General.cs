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
    public const string DEFAULT_SERVER_URL = "https://codescene.io";
    public const string DEFAULT_DEV_TOOLS_URL = "https://devtools.codescene.io";


    [Category("General")]
    [DisplayName("Enable Review Code Lenses")]
    [Description("Show CodeLenses for review diagnostics")]
    public bool EnableCodeLenses { get; set; } = true;

    //[Category("General")]
    //[DisplayName("Preview Code Health Gate")]
    //[Description("Preview the experimental Code Health Gate (beta)")]
    //public bool PreviewCodeHealthGate { get; set; } = true;

    [Category("General")]
    [DisplayName("Gitignore")]
    [Description("Exclude files in .gitignore from analysis")]
    public bool Gitignore { get; set; } = true;

    [Category("General")]
    [DisplayName("Enable Auto Refactor")]
    [Description("Enable CodeScene ACE. This is currently only available for customers part of the ACE beta program")]
    public bool EnableAutoRefactor { get; set; } = false;

    [Category("General")]
    [DisplayName("Server Url")]
    [Description("URL of the CodeScene server")]
    public string ServerUrl { get; set; } = DEFAULT_SERVER_URL;

    [Category("General")]
    [DisplayName("Devtools Portal Url")]
    [Description("URL of the CodeScene Devtool Portal server")]
    public string DevtoolsPortalUrl { get; set; } = DEFAULT_DEV_TOOLS_URL;

    public General() : base()
    {
        Saved += delegate { VS.StatusBar.ShowMessageAsync("Options Saved").FireAndForget(); };
    }
}
