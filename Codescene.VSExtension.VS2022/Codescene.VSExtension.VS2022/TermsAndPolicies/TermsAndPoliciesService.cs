using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using System;
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

    private bool _termsAccepted = false;
    private bool _infoBarShownOnce = false;
    private IVsInfoBarUIElement? _currentTermsInfoBarUiElement;

    private static readonly IVsInfoBarActionItem[] actionItems =
        [
            new InfoBarButton(CodeSceneConstants.Titles.ACCEPT_TERMS),
            new InfoBarButton(CodeSceneConstants.Titles.DECLINE_TERMS),
            new InfoBarHyperlink(CodeSceneConstants.Titles.VIEW_TERMS)
        ];
    private static readonly InfoBarModel model = new(
            [new InfoBarTextSpan(CodeSceneConstants.Titles.TERMS_INFO)],
            actionItems,
            KnownMonikers.StatusInformation,
            isCloseButtonVisible: false
        );

    public async Task<bool> EvaulateTermsAndPoliciesAcceptanceAsync()
    {
        if (_termsAccepted) return _termsAccepted;

        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var termsAccepted = GetAcceptedTerms();
            if (termsAccepted) _termsAccepted = termsAccepted; // Update cache state

            var factory = Package.GetGlobalService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
            var uiElement = factory?.CreateInfoBar(model);

            if (ShouldSkipInfoBar(termsAccepted, factory, uiElement))
                return termsAccepted;

            AddInfoBar(uiElement);

            return termsAccepted;
        }
        catch (Exception e)
        {
            _logger?.Error("Failed to evaluate Terms & Policies acceptance.", e);
            return false;
        }
    }

    private bool ShouldSkipInfoBar(bool termsAccepted, IVsInfoBarUIFactory factory, IVsInfoBarUIElement uiElement)
    {
        var setupIssue = _currentTermsInfoBarUiElement != null || factory == null || uiElement == null;

        return setupIssue || termsAccepted || _infoBarShownOnce;
    }

    private void AddInfoBar(IVsInfoBarUIElement uiElement)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        _currentTermsInfoBarUiElement = uiElement;
        uiElement.Advise(this, out _);

        if (TryGetMainInfoBarHost(out var mainHost))
        {
            mainHost.AddInfoBar(uiElement);
            _infoBarShownOnce = true;
            SendTelemetry(CodeSceneConstants.Telemetry.TERMS_AND_POLICIES_SHOWN);
        }
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

                _logger.Info($"User has {(hasAccepted ? "accepted" : "declined")} Terms & Policies. {(hasAccepted ? "Analysis will run when the file is updated or reopened." : "")}");

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

    private bool TryGetMainInfoBarHost(out IVsInfoBarHost host)
    {
        host = null;

        if (Package.GetGlobalService(typeof(SVsShell)) is not IVsShell vsShell)
            return false;

        ThreadHelper.ThrowIfNotOnUIThread();

        if (vsShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj) != VSConstants.S_OK)
            return false;

        if (obj is not IVsInfoBarHost infoBarHost)
            return false;

        host = infoBarHost;
        return true;
    }

    private void SendTelemetry(string eventName, string selection = "")
    {
        Task.Run(async () =>
        {
            Dictionary<string, object> additionalData = null;
            if (!string.IsNullOrEmpty(selection))
                additionalData = new Dictionary<string, object> { { "selection", selection } };

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
