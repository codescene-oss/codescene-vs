using CodesceneReeinventTest.Commands;
using System.Diagnostics;

namespace CodesceneReeinventTest;

internal class OpenCodesceneSiteCommand : VsCommandBase
{
    internal const int Id = PackageIds.OpenCodesceneSiteCommand;

    protected override async void InvokeInternal()
    {
        var options = General.Instance;
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
