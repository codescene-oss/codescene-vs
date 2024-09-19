namespace CodesceneReeinventTest;

[Command(PackageIds.OpenProblemsWindowCommand)]
internal sealed class OpenProblemsWindowCommand : BaseCommand<OpenProblemsWindowCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await ProblemsWindow.ShowAsync();
    }
}
