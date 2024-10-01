using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using EnvDTE;

namespace CodesceneReeinventTest
{
    [Command(PackageIds.OpenStatusWindowCommand)]
    internal sealed class OpenStatusWindowCommand(DIToolkitPackage package) : BaseDICommand(package)
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await StatusWindow.ShowAsync();
        }
    }
}
