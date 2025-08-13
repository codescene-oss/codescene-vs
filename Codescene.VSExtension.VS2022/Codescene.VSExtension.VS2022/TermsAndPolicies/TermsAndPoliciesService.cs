using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using CodeSceneConstants = Codescene.VSExtension.Core.Application.Services.Util.Constants;

namespace Codescene.VSExtension.VS2022.TermsAndPolicies;

[Export(typeof(TermsAndPoliciesService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class TermsAndPoliciesService : IVsInfoBarUIEvents
{
    [Import]
    private readonly ILogger _logger;

    private bool _infoBarShownOnce = false;
    private IVsInfoBarUIElement? _currentTermsInfoBarUiElement;

    public async Task<bool> ShowTermsIfNeededAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var termsAccepted = GetAcceptedTerms();
        var factory = Package.GetGlobalService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;

        var skipInfoBar = termsAccepted || _currentTermsInfoBarUiElement != null || factory == null || _infoBarShownOnce;
        if (skipInfoBar) return termsAccepted;

        IVsInfoBarActionItem[] actionItems =
        {
            new InfoBarButton(CodeSceneConstants.Titles.ACCEPT_TERMS),
            new InfoBarButton(CodeSceneConstants.Titles.DECLINE_TERMS),
            new InfoBarHyperlink(CodeSceneConstants.Titles.VIEW_TERMS)
        };

        var model = new InfoBarModel(
            [new InfoBarTextSpan("By using this extension you agree to CodeScene's Terms and Privacy Policy")],
            actionItems,
            KnownMonikers.StatusInformation,
            isCloseButtonVisible: false
        );

        var uiElement = factory.CreateInfoBar(model);
        if (uiElement == null)
            return termsAccepted;

        _currentTermsInfoBarUiElement = uiElement;
        uiElement.Advise(this, out _);

        var vsShell = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
        if (vsShell != null &&
            vsShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj) == VSConstants.S_OK &&
            obj is IVsInfoBarHost mainHost)
        {
            mainHost.AddInfoBar(uiElement);
            _infoBarShownOnce = true;
        }

        return termsAccepted;
    }

    public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
    {
        _logger.Debug("Terms & Policies bar has been closed.");
    }

    public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        SendTelemetry(CodeSceneConstants.Telemetry.TERMS_AND_POLICIES_RESPONSE, actionItem.Text);

        switch (actionItem.Text)
        {
            case CodeSceneConstants.Titles.ACCEPT_TERMS:
            case CodeSceneConstants.Titles.DECLINE_TERMS:
                var hasAccepted = actionItem.Text == CodeSceneConstants.Titles.ACCEPT_TERMS;
                SetAcceptedTerms(hasAccepted);

                _logger.Info($"User has {(hasAccepted ? "accepted" : "declined")} Terms & Conditions.");

                infoBarUIElement.Close();
                break;
            case CodeSceneConstants.Titles.VIEW_TERMS:
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://codescene.com/policies",
                    UseShellExecute = true
                });
                break;
        }
    }
    private void SendTelemetry(string eventName, string selection = "")
    {
        Task.Run(async () =>
        {
            var additionalData = new Dictionary<string, object>
            {
                { "selection", selection }
            };

            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(eventName, additionalData);
        }).FireAndForget();
    }

    private bool GetAcceptedTerms()
    {
        var store = GetOrCreateSettingsStore();

        return store.PropertyExists(CodeSceneConstants.Titles.SETTINGS_COLLECTION, CodeSceneConstants.Titles.ACCEPTED_TERMS_PROPERTY) &&
               store.GetBoolean(CodeSceneConstants.Titles.SETTINGS_COLLECTION, CodeSceneConstants.Titles.ACCEPTED_TERMS_PROPERTY);
    }

    private void SetAcceptedTerms(bool value)
    {
        var store = GetOrCreateSettingsStore();

        store.SetBoolean(CodeSceneConstants.Titles.SETTINGS_COLLECTION, CodeSceneConstants.Titles.ACCEPTED_TERMS_PROPERTY, value);
        _currentTermsInfoBarUiElement = null;
    }

    private WritableSettingsStore GetOrCreateSettingsStore()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(CodeSceneConstants.Titles.SETTINGS_COLLECTION))
            store.CreateCollection(CodeSceneConstants.Titles.SETTINGS_COLLECTION);

        return store;
    }
}
