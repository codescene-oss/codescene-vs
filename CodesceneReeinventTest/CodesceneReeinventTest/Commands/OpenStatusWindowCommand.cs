using CodesceneReeinventTest.Application;
using CodesceneReeinventTest.Application.Services.Authentication;
using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using EnvDTE;

namespace CodesceneReeinventTest
{
    [Command(PackageIds.OpenStatusWindowCommand)]
    internal sealed class OpenStatusWindowCommand(DIToolkitPackage package, IAuthenticationService authenticationService) : BaseDICommand(package)
    {
        private readonly IAuthenticationService _authenticationService = authenticationService;
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            //ovo rješenje nije ok jer šta ako je tool window docked od ranije a ne pozvan sa komandom
            //StatusWindowControl.SetAuthenticationService(_authenticationService);
            await StatusWindow.ShowAsync();
        }
    }
}
