using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Threading.Tasks;
using CodeSceneConstants = Codescene.VSExtension.Core.Application.Services.Util.Constants;

namespace Codescene.VSExtension.VS2022.TermsAndPolicies;

public class TermsAndPoliciesService
{
    private readonly IServiceProvider _serviceProvider;
    private IVsInfoBarUIElement? _currentTermsInfoBarUiElement;

    public static readonly string SettingsCollection = "CodeSceneExtension";
    public static readonly string AcceptedTermsProperty = "AcceptedTerms";

    public TermsAndPoliciesService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ShowTermsIfNeededAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        // InfoBar already showing, don't add another:
        if (_currentTermsInfoBarUiElement != null) return;

        // Terms have already been accepted:
        if (GetAcceptedTerms()) return;

        if (_serviceProvider.GetService(typeof(SVsInfoBarUIFactory)) is not IVsInfoBarUIFactory factory)
            return;

        IVsInfoBarActionItem[] actionItems =
        {
            new InfoBarButton(CodeSceneConstants.Titles.AcceptTerms),
            new InfoBarButton(CodeSceneConstants.Titles.DeclineTerms),
            new InfoBarHyperlink(CodeSceneConstants.Titles.ViewTerms)
        };

        var model = new InfoBarModel(
            [new InfoBarTextSpan("By using this extension you agree to CodeScene's Terms and Privacy Policy")],
            actionItems,
            KnownMonikers.StatusInformation,
            isCloseButtonVisible: false
        );

        var uiElement = factory.CreateInfoBar(model);
        if (uiElement == null)
            return;

        _currentTermsInfoBarUiElement = uiElement;
        uiElement.Advise(new InfoBarEvents(SetAcceptedTerms), out _);

        if (_serviceProvider.GetService(typeof(SVsShell)) is IVsShell vsShell &&
            vsShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj) == VSConstants.S_OK &&
            obj is IVsInfoBarHost mainHost)
        {
            mainHost.AddInfoBar(uiElement);
        }
    }

    private bool GetAcceptedTerms()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var settingsManager = new ShellSettingsManager(_serviceProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(SettingsCollection))
            store.CreateCollection(SettingsCollection);

        return store.PropertyExists(SettingsCollection, AcceptedTermsProperty) &&
               store.GetBoolean(SettingsCollection, AcceptedTermsProperty);
    }

    private void SetAcceptedTerms(bool value)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var settingsManager = new ShellSettingsManager(_serviceProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(SettingsCollection))
            store.CreateCollection(SettingsCollection);

        store.SetBoolean(SettingsCollection, AcceptedTermsProperty, value);
        _currentTermsInfoBarUiElement = null;
    }
}
