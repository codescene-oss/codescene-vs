using CodesceneReeinventTest.Commands;
using Core.Application.Services.Authentication;
using Core.Application.Services.ErrorHandling;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodesceneReeinventTest;

internal class SignOutCommand(IAuthenticationService authService, IErrorsHandler errorsHandler) : VsCommandBase
{
    internal const int Id = PackageIds.SignOutCommand;

    protected override void InvokeInternal()
    {
        _ = SignOutAsync();
    }
    private async Task SignOutAsync()
    {
        try
        {
            var username = authService.GetData().Name;
            var message = $"The account '{username}' has been used by:\n\nCodeScene\n\nSign out from these extensions?";
            var confimed = await VS.MessageBox.ShowConfirmAsync(message);
            if (!confimed)
            {
                return;
            }

            authService.SignOut();
            await ShowStatusAsync();
        }
        catch (Exception ex)
        {
            await errorsHandler.LogAsync("Signing out failed", ex);
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
