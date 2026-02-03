using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace Codescene.VSExtension.VS2022.Commands;

// [Command(PackageGuids.CodeSceneCmdSetString, PackageIds.RevokeTerms)]
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
        logger.Info("Terms & Policies revoked. Existing analysis results will be cleared upon file update or reopening. You will be prompted to accept CodeScene's Terms & Policies the next time the extension loads.");

        var cache = new ReviewCacheService();
        cache.Clear();

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
