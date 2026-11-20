// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces.Auth;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthCommandActions
    {
        public const string InactiveMessage = "Browser sign-in is experimental. Turn on Tools > Options > CodeScene > General > Browser sign-in to use it.";
        public const string SettingTokenMessage = "You are signed in with a personal access token. Remove it from Tools > Options > CodeScene > General to sign out.";
        public const string EnvTokenMessage = "You are signed in with the CS_ACCESS_TOKEN environment variable. Unset it and restart Visual Studio to sign out.";
        public const string NotSignedInMessage = "Not signed in to CodeScene.";

        public enum Command
        {
            SignIn,
            SignOut,
            OpenAccount,
            Reauthorize,
        }

        public static bool IsVisible(Command command, AuthSnapshot snapshot)
        {
            if (command == Command.Reauthorize)
            {
                return snapshot.Active && AuthStatusFields.ReauthorizeNeeded(snapshot.Status);
            }

            return snapshot.Active;
        }

        public static async Task SignInAsync(IAuthService service, IAuthHost host)
        {
            if (!service.Current.Active)
            {
                await host.ShowMessageAsync(InactiveMessage);
                return;
            }

            service.OpenAuth(null);

            var state = AuthStatusFields.State(service.Current.Status);
            if (state != AuthConstants.States.SIGNEDIN)
            {
                await service.SignInAsync();
            }
        }

        public static async Task SignOutAsync(IAuthService service, IAuthHost host)
        {
            if (!service.Current.Active)
            {
                await host.ShowMessageAsync(InactiveMessage);
                return;
            }

            var credentialSource = AuthStatusFields.CredentialSource(service.Current.Status);

            if (credentialSource == "oauth")
            {
                await service.SignOutAsync();
            }
            else if (credentialSource == "setting-token")
            {
                await host.ShowMessageAsync(SettingTokenMessage);
            }
            else if (credentialSource == "env-token")
            {
                await host.ShowMessageAsync(EnvTokenMessage);
            }
            else
            {
                await host.ShowMessageAsync(NotSignedInMessage);
            }
        }

        public static async Task OpenAccountAsync(IAuthService service, IAuthHost host)
        {
            if (!service.Current.Active)
            {
                await host.ShowMessageAsync(InactiveMessage);
                return;
            }

            service.OpenAuth(null);
        }

        public static async Task ReauthorizeAsync(IAuthService service, IAuthHost host)
        {
            if (!service.Current.Active)
            {
                await host.ShowMessageAsync(InactiveMessage);
                return;
            }

            if (IsVisible(Command.Reauthorize, service.Current))
            {
                await service.ReauthorizeAsync();
            }
        }
    }
}
