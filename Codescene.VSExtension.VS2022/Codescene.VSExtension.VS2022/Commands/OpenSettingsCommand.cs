// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.VS2022.Options;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

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
        var package = VS2022Package.Instance;
        if (package == null)
        {
            return;
        }

        package.JoinableTaskFactory.RunAsync(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            await telemetryManager.SendTelemetryAsync(Constants.Telemetry.OPENSETTINGS, cancellationToken: package.PackageDisposalToken);
        }).FileAndForget("OpenSettingsCommand/Execute");
    }
}
