using CodesceneReeinventTest.Commands;
using Core.Application.Services.Authentication;
using Core.Application.Services.ErrorHandling;

namespace CodesceneReeinventTest;

internal class OpenCodesceneSiteCommand(IAuthenticationService authService, IErrorsHandler errorsHandler) : VsCommandBase
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
            var loggedIn = authService.Login(url);
            if (!loggedIn)
            {
                await VS.MessageBox.ShowWarningAsync("Error", $"Auth rejected!");
            }

            var data = authService.GetData();
            await VS.MessageBox.ShowConfirmAsync("Auth successful", $"name:{data.Name}");
        }
        catch (Exception ex)
        {
            await errorsHandler.LogAsync("Authentication failed", ex);
            await VS.MessageBox.ShowWarningAsync("Error", $"Error: {ex.Message}");
        }
    }
}
