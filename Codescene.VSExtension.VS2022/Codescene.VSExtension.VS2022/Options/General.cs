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
    // TODO: Implement this feature, or remove it from the first official release.
    //[Category("General")]
    //[DisplayName("Gitignore")]
    //[Description("Exclude files in .gitignore from analysis")]
    //public bool Gitignore { get; set; } = true;

    [Category("General")]
    [DisplayName("Show Debug Logs")]
    [Description("Enable detailed debug logs in the CodeScene Output window")]
    public bool ShowDebugLogs { get; set; } = false;

    public General() : base()
    {
        Saved += delegate { VS.StatusBar.ShowMessageAsync("Options Saved").FireAndForget(); };
    }
}
