// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using CodeSceneConstants = Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.TermsAndPolicies;

[Export(typeof(TermsAndPoliciesService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class TermsAndPoliciesService : IVsInfoBarUIEvents
{
    private static readonly IVsInfoBarActionItem[] ActionItems =
    [
        new InfoBarButton(CodeSceneConstants.Titles.ACCEPTTERMS),
            new InfoBarButton(CodeSceneConstants.Titles.DECLINETERMS),
            new InfoBarHyperlink(CodeSceneConstants.Titles.VIEWTERMS)
    ];

    private static readonly InfoBarModel Model = new InfoBarModel(
            [new InfoBarTextSpan(CodeSceneConstants.Titles.TERMSINFO)],
            ActionItems,
            KnownMonikers.StatusInformation,
            isCloseButtonVisible: false);

    [Import]
    private readonly ILogger _logger;

    private bool _infoBarShownOnce;
    private IVsInfoBarUIElement _currentTermsInfoBarUiElement;

    public async Task<bool> EvaluateTermsAndPoliciesAcceptanceAsync()
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var termsAccepted = GetAcceptedTerms();
            var factory = Package.GetGlobalService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
            var uiElement = factory?.CreateInfoBar(Model);

            var setupIssue = _currentTermsInfoBarUiElement != null || factory == null || uiElement == null;
            var skipInfoBar = setupIssue || termsAccepted || _infoBarShownOnce;

            if (skipInfoBar)
            {
                return termsAccepted;
            }

            _currentTermsInfoBarUiElement = uiElement;
            uiElement.Advise(this, out _);

            if (!TryGetMainInfoBarHost(out var mainHost))
            {
                return false;
            }

            mainHost.AddInfoBar(uiElement);
            _infoBarShownOnce = true;
            SendTelemetry(CodeSceneConstants.Telemetry.TERMSANDPOLICIESSHOWN);

            return false;
        }
        catch (Exception e)
        {
            _logger?.Error("Failed to evaluate Terms & Policies acceptance.", e);
            return false;
        }
    }

    public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
    {
        _logger.Debug("Terms & Policies bar has been closed.");
    }

    public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        SendTelemetry(CodeSceneConstants.Telemetry.TERMSANDPOLICIESRESPONSE, actionItem.Text);

        switch (actionItem.Text)
        {
            case CodeSceneConstants.Titles.ACCEPTTERMS:
            case CodeSceneConstants.Titles.DECLINETERMS:
                var hasAccepted = actionItem.Text == CodeSceneConstants.Titles.ACCEPTTERMS;
                SetAcceptedTerms(hasAccepted);

                _logger.Info($"User has {(hasAccepted ? "accepted" : "declined")} Terms & Policies. {(hasAccepted ? "Analysis will run when the file is updated or reopened." : string.Empty)}", true);

                infoBarUIElement.Close();
                break;
            case CodeSceneConstants.Titles.VIEWTERMS:
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://codescene.com/policies",
                    UseShellExecute = true,
                });
                break;
        }
    }

    private bool TryGetMainInfoBarHost(out IVsInfoBarHost host)
    {
        host = null;

        if (Package.GetGlobalService(typeof(SVsShell)) is not IVsShell vsShell)
        {
            return false;
        }

        ThreadHelper.ThrowIfNotOnUIThread();

        if (vsShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj) != VSConstants.S_OK)
        {
            return false;
        }

        if (obj is not IVsInfoBarHost infoBarHost)
        {
            return false;
        }

        host = infoBarHost;
        return true;
    }

    private void SendTelemetry(string eventName, string selection = "")
    {
        Task.Run(async () =>
        {
            Dictionary<string, object> additionalData = null;
            if (!string.IsNullOrEmpty(selection))
            {
                additionalData = new Dictionary<string, object> { { "selection", selection } };
            }

            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            await telemetryManager.SendTelemetryAsync(eventName, additionalData);
        }).FireAndForget();
    }

    private bool GetAcceptedTerms()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var store = GetOrCreateSettingsStore();

        return store.PropertyExists(CodeSceneConstants.Titles.SETTINGSCOLLECTION, CodeSceneConstants.Titles.ACCEPTEDTERMSPROPERTY) &&
               store.GetBoolean(CodeSceneConstants.Titles.SETTINGSCOLLECTION, CodeSceneConstants.Titles.ACCEPTEDTERMSPROPERTY);
    }

    private void SetAcceptedTerms(bool value)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var store = GetOrCreateSettingsStore();

        store.SetBoolean(CodeSceneConstants.Titles.SETTINGSCOLLECTION, CodeSceneConstants.Titles.ACCEPTEDTERMSPROPERTY, value);
        _currentTermsInfoBarUiElement = null;
    }

    private WritableSettingsStore GetOrCreateSettingsStore()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(CodeSceneConstants.Titles.SETTINGSCOLLECTION))
        {
            store.CreateCollection(CodeSceneConstants.Titles.SETTINGSCOLLECTION);
        }

        return store;
    }
}
