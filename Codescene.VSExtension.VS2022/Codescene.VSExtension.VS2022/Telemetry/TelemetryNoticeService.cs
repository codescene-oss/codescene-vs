// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.VS2022.Options;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using CodeSceneConstants = Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.Notices;

[Export(typeof(TelemetryNoticeService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class TelemetryNoticeService : IVsInfoBarUIEvents
{
    private const string NoticeText = "CodeScene: Telemetry is enabled by default to help improve this extension. You can disable it in the settings.";
    private const string OpenSettingsAction = "Open Settings";
    private const string NoticeShownProperty = "TelemetryNoticeShown";

    private static readonly InfoBarModel Model = new InfoBarModel(
        [new InfoBarTextSpan(NoticeText)],
        [new InfoBarButton(OpenSettingsAction)],
        KnownMonikers.StatusInformation,
        isCloseButtonVisible: true);

    [Import]
    private readonly ILogger _logger;

    private bool _shownThisSession;

    public async Task ShowOnceAsync()
    {
        try
        {
            const int maxAttempts = 50;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (TryShow())
                {
                    return;
                }

                await Task.Delay(200).ConfigureAwait(false);
            }

            _logger?.Info("Telemetry notice could not be shown.");
        }
        catch (Exception e)
        {
            _logger?.Error("Failed to show telemetry notice.", e);
        }
    }

    public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
    {
    }

    public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!string.Equals(actionItem.Text, OpenSettingsAction, StringComparison.Ordinal))
        {
            return;
        }

        infoBarUIElement.Close();
        ThreadHelper.JoinableTaskFactory.RunAsync(OpenSettingsAsync).FileAndForget("TelemetryNoticeService/OpenSettings");
    }

    private async Task OpenSettingsAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();
    }

    private bool TryShow()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_shownThisSession || HasShownNotice())
        {
            _shownThisSession = true;
            return true;
        }

        var factory = Package.GetGlobalService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
        if (factory == null || !TryGetMainInfoBarHost(out var host))
        {
            return false;
        }

        var element = factory.CreateInfoBar(Model);
        if (element == null)
        {
            return false;
        }

        element.Advise(this, out _);
        host.AddInfoBar(element);
        _shownThisSession = true;
        SetNoticeShown();
        return true;
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

    private bool HasShownNotice()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var store = GetOrCreateSettingsStore();
        return store.PropertyExists(CodeSceneConstants.Titles.SETTINGSCOLLECTION, NoticeShownProperty)
            && store.GetBoolean(CodeSceneConstants.Titles.SETTINGSCOLLECTION, NoticeShownProperty);
    }

    private void SetNoticeShown()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var store = GetOrCreateSettingsStore();
        store.SetBoolean(CodeSceneConstants.Titles.SETTINGSCOLLECTION, NoticeShownProperty, true);
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
