using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.RevokeTerms)]
internal sealed class RevokeTermsCommand : BaseCommand<RevokeTermsCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var package = VS2022Package.Instance;
        var settingsManager = new ShellSettingsManager(package);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(Constants.Titles.SETTINGS_COLLECTION))
            store.CreateCollection(Constants.Titles.SETTINGS_COLLECTION);

        store.SetBoolean(Constants.Titles.SETTINGS_COLLECTION, Constants.Titles.ACCEPTED_TERMS_PROPERTY, false);

        var logger = await VS.GetMefServiceAsync<ILogger>();
        logger.Info("Terms and Policies revoked. You will be prompted to accept CodeScene's Terms & Policies the next time the extension loads.");

        SendTelemetry();
    }

    private void SendTelemetry()
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(Constants.Telemetry.REVOKE_TERMS);
        }).FireAndForget();
    }
}
