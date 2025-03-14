﻿using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application.Services.Authentication
{
    public delegate void AuthSignedInHandler(LoginResponse response);
    public delegate void AuthSignedOutHandler();
    public interface IAuthenticationService
    {
        bool IsLoggedIn();
        bool Login(string serverUrl);
        LoginResponse GetData();
        void SignOut();
        event AuthSignedInHandler OnSignedIn;
        event AuthSignedOutHandler OnSignedOut;
    }
}
