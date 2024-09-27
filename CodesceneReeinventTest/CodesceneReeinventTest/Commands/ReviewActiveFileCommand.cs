using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;

namespace CodesceneReeinventTest;

[Command(PackageIds.ReviewActiveFileCommand)]
internal sealed class ReviewActiveFileCommand : BaseDICommand
{
    public ReviewActiveFileCommand(DIToolkitPackage package) : base(package)
    {
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await VS.MessageBox.ShowAsync("Hello!");
        //await ProblemsWindow.ShowAsync();
    }
}
