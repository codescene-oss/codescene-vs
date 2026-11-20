// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using CodeSceneConstants = Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.Application.Auth
{
    [Export(typeof(SignInNudgeService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class SignInNudgeService : IVsInfoBarUIEvents
    {
        private const string NoticeText = "Sign in to CodeScene in your browser to connect the extension to your account.";
        private const string SignInAction = "Sign in";
        private const string DontShowAgainAction = "Don't show again";
        private const string DismissedProperty = "SignInNudgeDismissed";

        private static readonly InfoBarModel Model = new InfoBarModel(
            [new InfoBarTextSpan(NoticeText)],
            [new InfoBarButton(SignInAction), new InfoBarButton(DontShowAgainAction)],
            KnownMonikers.StatusInformation,
            isCloseButtonVisible: true);

        private readonly IAuthService _service;
        private readonly IAuthHost _host;
        private readonly ILogger _logger;

        private bool _shownThisSession;
        private IVsInfoBarUIElement _currentElement;

        [ImportingConstructor]
        public SignInNudgeService(IAuthService service, IAuthHost host, ILogger logger)
        {
            _service = service;
            _host = host;
            _logger = logger;
        }

        public void OnSnapshotChanged(AuthSnapshot snapshot)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    TryShow(snapshot);
                }
                catch (Exception e)
                {
                    _logger?.Error("Failed to evaluate sign-in nudge.", e);
                }
            }).FileAndForget("SignInNudgeService/OnSnapshotChanged");
        }

        public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (ReferenceEquals(_currentElement, infoBarUIElement))
            {
                _currentElement = null;
            }
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (string.Equals(actionItem.Text, SignInAction, StringComparison.Ordinal))
            {
                infoBarUIElement.Close();
                ThreadHelper.JoinableTaskFactory.RunAsync(SignInAsync).FileAndForget("SignInNudgeService/SignIn");
            }
            else if (string.Equals(actionItem.Text, DontShowAgainAction, StringComparison.Ordinal))
            {
                infoBarUIElement.Close();
                SetDismissed();
            }
        }

        private async System.Threading.Tasks.Task SignInAsync()
        {
            try
            {
                await AuthCommandActions.SignInAsync(_service, _host);
            }
            catch (Exception e)
            {
                _logger?.Error("Sign-in from nudge failed.", e);
            }
        }

        private void TryShow(AuthSnapshot snapshot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!SignInNudgePolicy.ShouldShow(snapshot, _shownThisSession, IsDismissed()))
            {
                return;
            }

            var factory = Package.GetGlobalService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
            if (factory == null || !TryGetMainInfoBarHost(out var host))
            {
                return;
            }

            var element = factory.CreateInfoBar(Model);
            if (element == null)
            {
                return;
            }

            element.Advise(this, out _);
            host.AddInfoBar(element);
            _shownThisSession = true;
            _currentElement = element;
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

        private bool IsDismissed()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var store = GetOrCreateSettingsStore();
                return store.PropertyExists(CodeSceneConstants.Titles.SETTINGSCOLLECTION, DismissedProperty)
                    && store.GetBoolean(CodeSceneConstants.Titles.SETTINGSCOLLECTION, DismissedProperty);
            }
            catch
            {
                return false;
            }
        }

        private void SetDismissed()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var store = GetOrCreateSettingsStore();
                store.SetBoolean(CodeSceneConstants.Titles.SETTINGSCOLLECTION, DismissedProperty, true);
            }
            catch (Exception e)
            {
                _logger?.Error("Failed to persist sign-in nudge dismissal.", e);
            }
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
}
