// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;

namespace Codescene.VSExtension.Core.Interfaces.Auth
{
    public interface IAuthService
    {
        event EventHandler<AuthSnapshot> SnapshotChanged;

        AuthSnapshot Current { get; }

        void Start();

        Task SetRepoAsync(AuthRepo repo);

        Task SignInAsync();

        Task ReauthorizeAsync();

        Task OpenBrowserAgainAsync();

        void OpenAuth(string screen);

        Task CloseAuthAsync();

        void SkipProject();

        void HideSignInBanner();

        Task SignOutAsync();

        Task RetryAsync();

        Task SelectAccountAsync(Newtonsoft.Json.Linq.JToken accountId);

        Task LinkProjectAsync(Newtonsoft.Json.Linq.JToken projectId);

        Task SetAccessTokenAsync(string token);

        Task RemoveAccessTokenAsync();

        Task OpenPatPageAsync();

        Task OpenServerSettingAsync();
    }
}
