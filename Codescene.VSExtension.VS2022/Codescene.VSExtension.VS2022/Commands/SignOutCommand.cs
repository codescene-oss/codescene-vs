// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Authentication;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022;

internal class SignOutCommand : Commands.VSBaseCommand
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger _logger;

    [ImportingConstructor]
    public SignOutCommand(IAuthenticationService authService, ILogger logger)
    {
        _authService = authService;
        _logger = logger;
    }

    internal const int Id = PackageIds.SignOutCommand;

    protected override void InvokeInternal()
    {
        _ = SignOutAsync();
    }

    private async Task SignOutAsync()
    {
        try
        {
            var username = _authService.GetData().Name;
            var message = $"The account '{username}' has been used by:\n\nCodeScene\n\nSign out from these extensions?";
            var confimed = await VS.MessageBox.ShowConfirmAsync(message);
            if (!confimed)
            {
                return;
            }

            _authService.SignOut();
            await ShowStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Signing out failed", ex);
        }
    }

    private async Task ShowStatusAsync(string message = "Successfully signed out.")
    {
        var model = new InfoBarModel([new InfoBarTextSpan(message),], KnownMonikers.PlayStepGroup, true);
        InfoBar infoBar = await VS.InfoBar.CreateAsync(ToolWindowGuids80.SolutionExplorer, model);
        await infoBar.TryShowInfoBarUIAsync();
        await VS.StatusBar.ShowProgressAsync(message, 2, 2);
        await VS.StatusBar.ShowMessageAsync(message);
    }
}
