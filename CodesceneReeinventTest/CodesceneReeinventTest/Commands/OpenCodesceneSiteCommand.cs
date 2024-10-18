using CodesceneReeinventTest.Commands;
using Core.Application.Services.Authentication;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;

namespace CodesceneReeinventTest;

internal class OpenCodesceneSiteCommand(IAuthenticationService authService) : VsCommandBase
{
    internal const int Id = PackageIds.OpenCodesceneSiteCommand;

    protected override async void InvokeInternal()
    {
        var options = General.Instance;
        var url = string.IsNullOrEmpty(options.ServerUrl) ? General.DEFAULT_SERVER_URL : options.ServerUrl;
        await OpenUrlAsync(url);
    }
    private async Task OpenUrlAsync(string url)
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
            await ShowSuccessStatusAsync();
        }
        catch (Exception ex)
        {
            await ShowFailedStatusAsync();
        }
    }
    private async Task ShowStartedStatusAsync()
    {
        IVsTaskStatusCenterService tsc = await VS.Services.GetTaskStatusCenterAsync();

        var options = default(TaskHandlerOptions);
        options.Title = "Signing in to CodeScene...";
        options.ActionsAfterCompletion = CompletionActions.None;

        TaskProgressData data = default;
        data.CanBeCanceled = true;

        ITaskHandler handler = tsc.PreRegister(options, data);
        await VS.StatusBar.ShowProgressAsync("Signing in to CodeScene...", 1, 2);
        await VS.StatusBar.ShowMessageAsync("Signing in to CodeScene...");
    }
    private async Task ShowSuccessStatusAsync()
    {
        var model = new InfoBarModel(
        new[] {
                    new InfoBarTextSpan("Signed in to CodeScene as "),
            //new InfoBarHyperlink("Click me")f
        },
        KnownMonikers.PlayStepGroup,
        true);

        InfoBar infoBar = await VS.InfoBar.CreateAsync(ToolWindowGuids80.SolutionExplorer, model);
        await infoBar.TryShowInfoBarUIAsync();
    }
    private async Task ShowFailedStatusAsync()
    {
        await VS.StatusBar.ShowProgressAsync("Signing in to CodeScene failed", 2, 2);
        await VS.StatusBar.ShowMessageAsync("Signing in to CodeScene failed");
    }
}
