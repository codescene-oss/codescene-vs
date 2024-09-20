using System.Diagnostics;

namespace CodesceneReeinventTest;

[Command(PackageIds.OpenCodesceneSiteCommand)]
internal sealed class OpenCodesceneSiteCommand : BaseCommand<OpenCodesceneSiteCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var options = await General.GetLiveInstanceAsync();
        var url = string.IsNullOrEmpty(options.ServerUrl) ? General.DEFAULT_SERVER_URL : options.ServerUrl;
        await OpenUrlAsync(url);
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
