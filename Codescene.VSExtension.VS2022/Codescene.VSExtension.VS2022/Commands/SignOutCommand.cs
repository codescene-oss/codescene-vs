using Codescene.VSExtension.Core.Application.Services.Authentication;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.Commands;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022;

internal class SignOutCommand(IAuthenticationService authService, ILogger errorsHandler) : Commands.VSBaseCommand
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
