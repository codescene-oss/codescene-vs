using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using EnvDTE;

namespace CodesceneReeinventTest
{
    [Command(PackageIds.OpenStatusWindowCommand)]
    internal sealed class OpenStatusWindowCommand : BaseDICommand
    {
        public OpenStatusWindowCommand(DIToolkitPackage package) : base(package)
        {
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await StatusWindow.ShowAsync();
        }
    }
}
