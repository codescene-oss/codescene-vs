using System.Diagnostics;

namespace CodesceneReeinventTest;

[Command(PackageIds.OpenCodesceneSiteCommand)]
internal sealed class OpenCodesceneSiteCommand : BaseCommand<OpenCodesceneSiteCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        //await VS.MessageBox.ShowWarningAsync("CodesceneReeinventTest", "Button clicked");
        await OpenUrlAsync("https://codescene.com/");
    }

    private async Task OpenUrlAsync(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true // Ovo osigurava da se koristi podrazumevani browser
            });
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowWarningAsync("Error", $"Error: {ex.Message}");
        }
    }

}
