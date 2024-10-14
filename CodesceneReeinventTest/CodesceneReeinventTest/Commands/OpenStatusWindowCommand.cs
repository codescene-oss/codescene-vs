using CodesceneReeinventTest.Commands;

namespace CodesceneReeinventTest;

internal class OpenStatusWindowCommand : VsCommandBase
{
    internal const int Id = PackageIds.OpenStatusWindowCommand;
    protected override async void InvokeInternal()
    {
        await StatusWindow.ShowAsync();
    }
}
