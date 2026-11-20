// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.VS2022.Options;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Application.Auth
{
    [Export(typeof(IAuthHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class VsAuthHost : IAuthHost
    {
        private readonly ILogger _logger;

        [ImportingConstructor]
        public VsAuthHost(ILogger logger)
        {
            _logger = logger;
        }

        public async Task OpenExternalAsync(string url)
        {
            if (!AuthUrlPolicy.IsOpenable(url))
            {
                _logger.Warn("[auth] refused to open a URL that is not http or https");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });

                _logger.Debug("[auth] opened the URL in the browser");
            }
            catch (Exception ex)
            {
                _logger.Warn("[auth] failed to open the URL in the browser: " + ex.GetType().Name);
            }
        }

        public async Task OpenServerSettingAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();
        }

        public async Task RevealMonitorAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await CodeSceneToolWindow.ShowAsync();
        }

        public async Task ShowMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await VS.MessageBox.ShowAsync("CodeScene", message, OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK);
        }
    }
}
