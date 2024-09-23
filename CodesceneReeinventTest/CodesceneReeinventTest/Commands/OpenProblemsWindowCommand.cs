using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;

namespace CodesceneReeinventTest;

[Command(PackageIds.OpenProblemsWindowCommand)]
internal sealed class OpenProblemsWindowCommand : BaseDICommand
{
    public OpenProblemsWindowCommand(DIToolkitPackage package) : base(package)
    {
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await ProblemsWindow.ShowAsync();
    }
}
