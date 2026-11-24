// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.TestConnectivityCommand)]
internal sealed class TestConnectivityCommand : BaseCommand<TestConnectivityCommand>
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

            if (!await EnsureUsableAsync())
            {
                return;
            }

            var connectivityTester = await VS.GetMefServiceAsync<IAgentConnectivityTester>();
            if (connectivityTester == null)
            {
                logger?.Warn("Failed to get connectivity tester service.");
                return;
            }

            var workDir = await GetWorkDirectoryAsync();

            var dialog = await StartWaitDialogAsync(logger);
            if (dialog != null)
            {
                await TestWithDialogAsync(connectivityTester, workDir, dialog);
            }
        }
        catch (Exception ex)
        {
            logger?.Error("Failed to execute test connectivity command.", ex);
        }
    }

    private static async Task<bool> EnsureUsableAsync()
    {
        var agentAvailability = await VS.GetMefServiceAsync<IAgentAvailability>();
        if (agentAvailability != null && agentAvailability.Usable)
        {
            return true;
        }

        await VS.MessageBox.ShowAsync(
            "CodeScene",
            ProviderApiKeyActions.ConnectivityNotUsableMessage,
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK);
        return false;
    }

    private static async Task<IVsThreadedWaitDialog2> StartWaitDialogAsync(ILogger logger)
    {
        var factory = (IVsThreadedWaitDialogFactory)await VS.Services.GetThreadedWaitDialogAsync();
        if (factory == null)
        {
            logger?.Warn("Failed to get threaded wait dialog factory.");
            return null;
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        factory.CreateInstance(out var dialog);

        if (dialog == null)
        {
            logger?.Warn("Failed to create threaded wait dialog.");
            return null;
        }

        dialog.StartWaitDialogWithPercentageProgress(
            "CodeScene",
            "Testing provider connectivity...",
            null,
            null,
            null,
            true,
            0,
            0,
            0);
        return dialog;
    }

    private static async Task TestWithDialogAsync(IAgentConnectivityTester connectivityTester, string workDir, IVsThreadedWaitDialog2 dialog)
    {
        try
        {
            var result = await connectivityTester.TestAsync(workDir, CancellationToken.None);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            dialog.EndWaitDialog(out _);

            var message = ProviderApiKeyActions.ConnectivityReport(result);
            if (!string.IsNullOrEmpty(message))
            {
                var icon = result.Success ? OLEMSGICON.OLEMSGICON_INFO : OLEMSGICON.OLEMSGICON_CRITICAL;
                await VS.MessageBox.ShowAsync("CodeScene", message, icon, OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
        }
        catch
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            dialog.EndWaitDialog(out _);
            throw;
        }
    }

    private async Task<string> GetWorkDirectoryAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var solution = await VS.Solutions.GetCurrentSolutionAsync();
        if (solution != null && !string.IsNullOrEmpty(solution.FullPath))
        {
            return Path.GetDirectoryName(solution.FullPath);
        }

        var docView = await VS.Documents.GetActiveDocumentViewAsync();
        if (docView?.FilePath != null)
        {
            return Path.GetDirectoryName(docView.FilePath);
        }

        return Path.GetTempPath();
    }
}
