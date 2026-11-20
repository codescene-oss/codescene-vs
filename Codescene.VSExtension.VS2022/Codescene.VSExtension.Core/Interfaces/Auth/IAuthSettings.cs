// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Auth
{
    public interface IAuthSettings
    {
        event EventHandler Changed;

        bool BrowserSignInEnabled { get; }

        string ServerUrl { get; }

        string AccessToken { get; }

        Task SetAccessTokenAsync(string token);
    }
}
