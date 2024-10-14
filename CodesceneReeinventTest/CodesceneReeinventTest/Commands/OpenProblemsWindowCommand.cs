using CodesceneReeinventTest.Commands;
using CodesceneReeinventTest.ToolWindows.Problems;

namespace CodesceneReeinventTest;

internal sealed class OpenProblemsWindowCommand : VsCommandBase
{
    internal const int Id = PackageIds.OpenProblemsWindowCommand;

    protected override async void InvokeInternal()
    {
        await ProblemsWindow.ShowAsync();
    }
}
