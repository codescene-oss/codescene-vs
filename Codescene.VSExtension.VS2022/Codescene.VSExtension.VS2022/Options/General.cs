using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
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
    // TODO: Implement this feature, or remove it from the first official release.
    //[Category("General")]
    //[DisplayName("Gitignore")]
    //[Description("Exclude files in .gitignore from analysis")]
    //public bool Gitignore { get; set; } = true;

    [Category("General")]
    [DisplayName("Show Debug Logs")]
    [Description("Enable detailed debug logs in the CodeScene Output window")]
    public bool ShowDebugLogs { get; set; } = false;

    private bool _enableAutoRefactor = true;

    [Category("General")]
    [DisplayName("Enable Auto Refactor")]
    [Description("Enable CodeScene ACE")]
    public bool EnableAutoRefactor
    {
        get => _enableAutoRefactor;
        set
        {
            if (_enableAutoRefactor != value)
            {
                _enableAutoRefactor = value;
                OnEnableAutoRefactorChanged();
            }
        }
    }

    //[Category("General")]
    //[DisplayName("Server Url")]
    //[Description("URL of the CodeScene server")]
    //public string ServerUrl { get; set; } = DEFAULT_SERVER_URL;

    //[Category("General")]
    //[DisplayName("Devtools Portal Url")]
    //[Description("URL of the CodeScene Devtool Portal server")]
    //public string DevtoolsPortalUrl { get; set; } = DEFAULT_DEV_TOOLS_URL;

    public General() : base()
    {
        Saved += delegate { VS.StatusBar.ShowMessageAsync("Options Saved").FireAndForget(); };
    }

    private async void OnEnableAutoRefactorChanged()
    {
        try
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            preflightManager.RunPreflight(true);
        }
        catch (Exception ex)
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();
            logger.Error("Error running preflight after changing EnableAutoRefactor setting", ex);
		}
    }
}
