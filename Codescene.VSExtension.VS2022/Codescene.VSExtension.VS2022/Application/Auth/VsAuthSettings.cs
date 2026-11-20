// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.VS2022.Options;

namespace Codescene.VSExtension.VS2022.Application.Auth
{
    [Export(typeof(IAuthSettings))]
    internal class VsAuthSettings : IAuthSettings
    {
        public VsAuthSettings()
        {
            General.AuthSettingsChanged += OnAuthSettingsChanged;
        }

        public event EventHandler Changed;

        public bool BrowserSignInEnabled => General.Instance.BrowserSignIn;

        public string ServerUrl => General.Instance.ServerUrl;

        public string AccessToken => General.Instance.AuthToken;

        public async Task SetAccessTokenAsync(string token)
        {
            var trimmed = token?.Trim();
            General.Instance.AuthToken = string.IsNullOrEmpty(trimmed) ? string.Empty : trimmed;
            await General.Instance.SaveAsync();
        }

        private void OnAuthSettingsChanged(object sender, EventArgs e)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
