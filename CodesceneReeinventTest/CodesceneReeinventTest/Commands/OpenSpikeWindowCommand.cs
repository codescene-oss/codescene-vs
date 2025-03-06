using CodesceneReeinventTest.Commands;
using CodesceneReeinventTest.ToolWindows.Status;

namespace CodesceneReeinventTest;

internal class OpenSpikeWindowCommand : VsCommandBase
{
    internal const int Id = PackageIds.OpenSpikeWindowCommand;
    protected override async void InvokeInternal()
    {
        await SpikeWindow.ShowAsync();
    }
}
