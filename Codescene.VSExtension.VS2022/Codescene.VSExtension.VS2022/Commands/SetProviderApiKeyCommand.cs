// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.VS2022.Controls;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.SetProviderApiKeyCommand)]
internal sealed class SetProviderApiKeyCommand : BaseCommand<SetProviderApiKeyCommand>
{
    private IAgentAvailability _agentAvailability;

    protected override void BeforeQueryStatus(EventArgs e)
    {
        _agentAvailability ??= ThreadHelper.JoinableTaskFactory.Run(async () => await VS.GetMefServiceAsync<IAgentAvailability>());

        if (_agentAvailability != null)
        {
            Command.Visible = _agentAvailability.Enabled;
        }
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();

        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var store = await VS.GetMefServiceAsync<IProviderKeyStore>();
            if (store == null)
            {
                logger?.Warn("Failed to get provider key store service.");
                return;
            }

            var dialog = new ProviderApiKeyDialog(store);
            dialog.ShowModal();
        }
        catch (Exception ex)
        {
            logger?.Error("Failed to execute set provider API key command.", ex);
        }
    }
}
