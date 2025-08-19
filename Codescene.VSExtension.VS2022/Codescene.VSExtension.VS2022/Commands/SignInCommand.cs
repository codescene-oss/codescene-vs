using Codescene.VSExtension.Core.Application.Services.Authentication;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.Commands;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022;

internal class SignInCommand(IAuthenticationService authService, ILogger errorsHandler) : Commands.VSBaseCommand
{
    internal const int Id = PackageIds.SignInCommand;

    protected override void InvokeInternal()
    {
        //var options = General.Instance;
        //var url = string.IsNullOrEmpty(options.ServerUrl) ? General.DEFAULT_SERVER_URL : options.ServerUrl;
        //_ = SignInAsync(url);
    }
    private async Task SignInAsync(string url)
    {
        await ShowStartedStatusAsync();
        try
        {
            var loggedIn = authService.Login(url);
            if (!loggedIn)
            {
                await ShowFailedStatusAsync();
            }

            var data = authService.GetData();
            await ShowSuccessStatusAsync(data);
        }
        catch (Exception ex)
        {
            errorsHandler.Error("Authentication failed", ex);
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
