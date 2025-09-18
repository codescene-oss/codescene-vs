using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Git;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using CodeSceneConstants = Codescene.VSExtension.Core.Application.Services.Util.Constants;

namespace Codescene.VSExtension.VS2022.CommitBaseline;

[Export(typeof(CommitBaselineService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CommitBaselineService
{
    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly IGitService _gitService;

    public string ResolveBaseline(string repoPath, CommitBaselineType baselineType)
    {
        var currentBranch = _gitService.GetCurrentBranch(repoPath);
        var headCommit = _gitService.GetHeadCommit(repoPath);

        return baselineType switch
        {
            CommitBaselineType.Head => headCommit,

            CommitBaselineType.BranchCreate =>
                _gitService.GetBranchCreationCommit(repoPath, currentBranch),

            CommitBaselineType.Default =>
                currentBranch == _gitService.GetDefaultBranch(repoPath)
                    ? headCommit
                    : _gitService.GetBranchCreationCommit(repoPath, currentBranch),

            _ => ""
        };
    }

    public void OnCommitBaselineChanged(string commitBaselineType)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        SendTelemetry(CodeSceneConstants.Telemetry.COMMIT_BASELINE_CHANGED, commitBaselineType);

        SetCommitBaseline(commitBaselineType);

    }

    private void SendTelemetry(string eventName, string selection = "")
    {
        Task.Run(async () =>
        {
            Dictionary<string, object> additionalData = null;
            if (!string.IsNullOrEmpty(selection))
                additionalData = new Dictionary<string, object> { { "selection", selection } };

            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(eventName, additionalData);
        }).FireAndForget();
    }

    public string GetCommitBaseline()
    {
        var store = GetOrCreateSettingsStore();

        return store.PropertyExists(CodeSceneConstants.Titles.SETTINGS_COLLECTION, CodeSceneConstants.Titles.COMMIT_BASELINE) ?
               store.GetString(CodeSceneConstants.Titles.SETTINGS_COLLECTION, CodeSceneConstants.Titles.COMMIT_BASELINE) : CommitBaselineType.Default.ToString();
    }

    public void SetCommitBaseline(string value)
    {
        var store = GetOrCreateSettingsStore();

        store.SetString(CodeSceneConstants.Titles.SETTINGS_COLLECTION, CodeSceneConstants.Titles.COMMIT_BASELINE, value);
    }

    private WritableSettingsStore GetOrCreateSettingsStore()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(CodeSceneConstants.Titles.SETTINGS_COLLECTION))
            store.CreateCollection(CodeSceneConstants.Titles.SETTINGS_COLLECTION);

        return store;
    }
}
