using CodesceneReeinventTest.Commands;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Core.Application.Services.Authentication;

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
        try
        {
            await StartAsync(url);
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowWarningAsync("Error", $"Error: {ex.Message}");
        }
    }
    private async Task StartAsync(string url)
    {
        await ShowStartedStatusAsync();

        string loginUrl = await GetLoginUrlAsync();

        System.Diagnostics.Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        //do try catch
        LoginResponse loginResponse = await HandleUriAsync("https://codescene.io/configuration/devtools-tokens/add/vscode/", cts.Token);

    }
    public async Task<LoginResponse> HandleUriAsync(string redirectUri, CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        try
        {
            var loggedIn = authService.Login(url);
            if (!loggedIn)
            {
                await VS.MessageBox.ShowWarningAsync("Error", $"Auth rejected!");
            }

            var data = authService.GetData();
            await ShowSuccessStatusAsync();
        }
        catch (Exception ex)
        {
            await ShowFailedStatusAsync();
            return null;
        }
        finally
        {
            listener.Stop();
        }
    }
    public class LoginResponse
    {
        public string Name { get; }
        public string Token { get; }
        public string UserId { get; }

        public LoginResponse(string name, string token, string userId)
        {
            Name = name;
            Token = token;
            UserId = userId;
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
    private async Task<string> GetLoginUrlAsync()
    {
        return "https://codescene.io/login?next=/configuration/devtools-tokens/add/vscode";
    }
}
