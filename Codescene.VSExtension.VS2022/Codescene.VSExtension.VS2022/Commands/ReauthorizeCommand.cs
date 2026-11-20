// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.ReauthorizeCommand)]
internal sealed class ReauthorizeCommand : BaseCommand<ReauthorizeCommand>
{
    private IAuthService _authService;

    protected override void BeforeQueryStatus(EventArgs e)
    {
        _authService ??= ThreadHelper.JoinableTaskFactory.Run(async () => await VS.GetMefServiceAsync<IAuthService>());

        if (_authService != null)
        {
            Command.Visible = AuthCommandActions.IsVisible(AuthCommandActions.Command.Reauthorize, _authService.Current);
        }
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();

        try
        {
            var authService = await VS.GetMefServiceAsync<IAuthService>();
            var authHost = await VS.GetMefServiceAsync<IAuthHost>();

            await AuthCommandActions.ReauthorizeAsync(authService, authHost);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to execute reauthorize command.", ex);
        }
    }
}
