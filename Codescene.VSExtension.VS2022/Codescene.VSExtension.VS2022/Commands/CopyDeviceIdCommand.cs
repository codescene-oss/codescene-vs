using System;
using System.Threading.Tasks;
using System.Windows;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.CopyDeviceId)]
internal sealed class CopyDeviceIdCommand : BaseCommand<CopyDeviceIdCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();

        try
        {
            var deviceIdStore = await VS.GetMefServiceAsync<IDeviceIdStore>();
            var deviceId = deviceIdStore.GetDeviceId();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                Clipboard.SetText(deviceId);
                logger.Info("Device ID copied to clipboard.");
            }
            else
            {
                logger.Warn("Failed to retrieve device ID.");
            }
        }
        catch (Exception ex)
        {
            logger.Error("Failed to retrieve device ID.", ex);
        }
    }
}
