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

        public enum Command
        {
            SignIn,
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
