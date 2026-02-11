// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.VS2022.Application.Services;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class AceAcknowledgeToolWindow : BaseToolWindow<AceAcknowledgeToolWindow>
{
    private static WebComponentUserControl _ctrl;

    private static FnToRefactorModel _fnToRefactor;
    private static string _filePath;

    public override Type PaneType => typeof(Pane);

    public static bool IsCreated() => _ctrl != null;

    public static void UpdateRefactoringData(FnToRefactorModel fnToRefactor, string path)
    {
        _fnToRefactor = fnToRefactor;
        _filePath = path;
    }

    public static async Task UpdateViewAsync()
    {
        if (_ctrl == null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await ShowAsync();
            return;
        }

        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        var settingsProvider = await VS.GetMefServiceAsync<ISettingsProvider>();

        var hastoken = !string.IsNullOrWhiteSpace(settingsProvider.AuthToken);
        var message = new WebComponentMessage<AceAcknowledgeComponentData>
        {
            MessageType = WebComponentConstants.MessageTypes.UPDATERENDERER,
            Payload = new WebComponentPayload<AceAcknowledgeComponentData>
            {
                IdeType = WebComponentConstants.VISUALSTUDIOIDETYPE,
                View = WebComponentConstants.ViewTypes.ACEACKNOWLEDGE,
                Data = new AceAcknowledgeComponentData
                {
                    FilePath = _filePath,
                    AutoRefactor = new AutoRefactorConfig
                    {
                        Activated = acknowledgementStateService.IsAcknowledged(),
                        Disabled = !hastoken,
                        Visible = true,
                    },
                    FnToRefactor = _fnToRefactor,
                },
            },
        };

        await _ctrl.UpdateViewAsync(message);
        await ShowAsync();
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        var settingsProvider = await VS.GetMefServiceAsync<ISettingsProvider>();

        var hastoken = !string.IsNullOrWhiteSpace(settingsProvider.AuthToken);
        var payload = new WebComponentPayload<AceAcknowledgeComponentData>
        {
            IdeType = WebComponentConstants.VISUALSTUDIOIDETYPE,
            View = WebComponentConstants.ViewTypes.ACEACKNOWLEDGE,
            Data = new AceAcknowledgeComponentData
            {
                FilePath = _filePath,
                AutoRefactor = new AutoRefactorConfig
                {
                    Activated = acknowledgementStateService.IsAcknowledged(),
                    Disabled = !hastoken,
                    Visible = true,
                },
                FnToRefactor = _fnToRefactor,
            },
        };

        var ctrl = new WebComponentUserControl(payload, logger)
        {
            CloseRequested = async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await HideAsync();
            },
        };

        _ctrl = ctrl;

        return ctrl;
    }

    public override string GetTitle(int toolWindowId) => "ACE Acknowledgement";

    [Guid("B5AE467B-9A17-4496-95A7-87DCE4703275")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}
