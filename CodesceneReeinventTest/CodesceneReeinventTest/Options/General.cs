using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CodesceneReeinventTest;
internal partial class OptionsProvider
{
    // Register the options with this attribute on your package class:
    // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "CodesceneReeinventTest", "General", 0, 0, true, SupportsProfiles = true)]
    [ComVisible(true)]
    public class GeneralOptions : BaseOptionPage<General> { }
}

public class General : BaseOptionModel<General>
{
    public const string DEFAULT_SERVER_URL = "https://codescene.io";

    [Category("General")]
    [DisplayName("Enable CodeScene Lenses")]
    [Description("Enable CodeScene code lenses")]
    //[DefaultValue(true)] Emir, this doesn't work
    public bool EnableCodeLenses { get; set; } = true;

    [Category("General")]
    [DisplayName("Preview Code Health Gate")]
    [Description("Preview the experimental Code Health Gate (beta)")]
    public bool PreviewCodeHealthGate { get; set; } = false;

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
    [Description("CodeScene authentication server Url")]
    public string ServerUrl { get; set; } = DEFAULT_SERVER_URL;
}
