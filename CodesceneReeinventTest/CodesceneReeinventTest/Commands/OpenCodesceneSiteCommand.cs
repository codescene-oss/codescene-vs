using CodesceneReeinventTest.Commands;
using Core.Application.Services.Authentication;
using Core.Application.Services.ErrorHandling;
using Core.Models;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;

namespace CodesceneReeinventTest;

internal class OpenCodesceneSiteCommand(IAuthenticationService authService, IErrorsHandler errorsHandler) : VsCommandBase
{
    internal const int Id = PackageIds.OpenCodesceneSiteCommand;

    protected override void InvokeInternal()
    {
        var options = General.Instance;
        var url = string.IsNullOrEmpty(options.ServerUrl) ? General.DEFAULT_SERVER_URL : options.ServerUrl;
        _ = SignInAsync(url);
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
            await errorsHandler.LogAsync("Authentication failed", ex);
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

        ITaskHandler handler = tsc.PreRegister(options, data);
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
