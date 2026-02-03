// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Authentication;
using Codescene.VSExtension.Core.Models;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;

namespace Codescene.VSExtension.VS2022;

internal class SignInCommand : Commands.VSBaseCommand
{
    internal const int Id = PackageIds.SignInCommand;

    private readonly IAuthenticationService _authService;
    private readonly ILogger _logger;

    [ImportingConstructor]
    public SignInCommand(IAuthenticationService authService, ILogger logger)
    {
        _authService = authService;
        _logger = logger;
    }

    protected override void InvokeInternal()
    {
        // var options = General.Instance;
        // var url = string.IsNullOrEmpty(options.ServerUrl) ? General.DEFAULT_SERVER_URL : options.ServerUrl;
        // _ = SignInAsync(url);
    }

    private async Task SignInAsync(string url)
    {
        await ShowStartedStatusAsync();
        try
        {
            var loggedIn = _authService.Login(url);
            if (!loggedIn)
            {
                await ShowFailedStatusAsync();
            }

            var data = _authService.GetData();
            await ShowSuccessStatusAsync(data);
        }
        catch (Exception ex)
        {
            _logger.Error("Authentication failed", ex);
            await ShowFailedStatusAsync();
        }
    }

    private async Task ShowStartedStatusAsync(string message = "Signing in to CodeScene...")
    {
        IVsTaskStatusCenterService tsc = await VS.Services.GetTaskStatusCenterAsync();

        var options = default(TaskHandlerOptions);
        options.Title = message;
        options.ActionsAfterCompletion = CompletionActions.None;

        TaskProgressData data = default;
        data.CanBeCanceled = true;

        tsc.PreRegister(options, data);
        await VS.StatusBar.ShowProgressAsync(message, 1, 2);
        await VS.StatusBar.ShowMessageAsync(message);
    }

    private async Task ShowSuccessStatusAsync(LoginResponse response, string message = "Signed in to CodeScene as ")
    {
        message = $"{message} {response.Name}";
        var model = new InfoBarModel([new InfoBarTextSpan(message),], KnownMonikers.PlayStepGroup, true);

        InfoBar infoBar = await VS.InfoBar.CreateAsync(ToolWindowGuids80.SolutionExplorer, model);
        await infoBar.TryShowInfoBarUIAsync();
        await VS.StatusBar.ShowProgressAsync(message, 2, 2);
        await VS.StatusBar.ShowMessageAsync(message);
    }

    private async Task ShowFailedStatusAsync(string message = "Signing in to CodeScene failed")
    {
        await VS.StatusBar.ShowProgressAsync(message, 2, 2);
        await VS.StatusBar.ShowMessageAsync(message);
    }
}
