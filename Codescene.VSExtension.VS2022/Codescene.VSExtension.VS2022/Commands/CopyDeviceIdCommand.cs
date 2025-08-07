using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Threading.Tasks;
using System.Windows;

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
                logger.Warn("Failed to retrieve device ID.");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to retrieve device ID. Error message: {ex.Message}");
        }
    }
}