// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.VS2022.Options;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.OpenSettings)]
internal sealed class OpenSettingsCommand : BaseCommand<OpenSettingsCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        SendTelemetry();

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();
    }

    private void SendTelemetry()
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(Constants.Telemetry.OPENSETTINGS);
        }).FireAndForget();
    }
}
