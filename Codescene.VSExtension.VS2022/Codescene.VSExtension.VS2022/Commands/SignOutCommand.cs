// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.SignOutCommand)]
internal sealed class SignOutCommand : BaseCommand<SignOutCommand>
{
    private IAuthService _authService;

    protected override void BeforeQueryStatus(EventArgs e)
    {
        _authService ??= ThreadHelper.JoinableTaskFactory.Run(async () => await VS.GetMefServiceAsync<IAuthService>());

        if (_authService != null)
        {
            Command.Visible = AuthCommandActions.IsVisible(AuthCommandActions.Command.SignOut, _authService.Current);
        }
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();

        try
        {
            var authService = await VS.GetMefServiceAsync<IAuthService>();
            var authHost = await VS.GetMefServiceAsync<IAuthHost>();

            await AuthCommandActions.SignOutAsync(authService, authHost);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to execute sign out command.", ex);
        }
    }
}
