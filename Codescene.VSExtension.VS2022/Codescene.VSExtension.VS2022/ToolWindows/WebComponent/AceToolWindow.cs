// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Application.Refactoring;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class AceToolWindow : BaseToolWindow<AceToolWindow>
{
    private static WebComponentUserControl _ctrl;
    private static Core.Interfaces.Refactoring.IRefactoringOrchestrator _orchestrator;

    public string FilePath { get; set; }

    public override Type PaneType => typeof(Pane);

    public static bool IsCreated() => _ctrl != null;

    public static async Task UpdateViewAsync(WebComponentMessage<AceComponentData> message)
    {
        await _ctrl.UpdateViewAsync(message);
    }

    public static async Task CloseAsync()
    {
        var orchestrator = await VS.GetMefServiceAsync<Core.Interfaces.Refactoring.IRefactoringOrchestrator>();
        orchestrator?.Cancel();

        if (_ctrl is not null && _ctrl.CloseRequested is not null)
        {
            await _ctrl.CloseRequested();
        }
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<AceComponentMapper>();
        var orchestrator = await VS.GetMefServiceAsync<Core.Interfaces.Refactoring.IRefactoringOrchestrator>();
        _orchestrator = orchestrator;

        var session = orchestrator.Current;
        AceComponentData data;
        if (session != null)
        {
            data = mapper.MapSession(session);
        }
        else
        {
            var handler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();
            data = mapper.MapLoading(handler.Path, handler.RefactorableFunction);
        }

        var payload = WebComponentPayload<AceComponentData>.Create(
            WebComponentConstants.ViewTypes.ACE,
            data);

        var ctrl = new WebComponentUserControl(payload, logger)
        {
            CloseRequested = async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                orchestrator.Cancel();
                await HideAsync();
            },
        };

        _ctrl = ctrl;

        return ctrl;
    }

    public override string GetTitle(int toolWindowId) => Titles.CODESCENEAGENT;

    [Guid("60f71481-a161-4512-bb43-162b852a86d1")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;

        protected override void OnClose()
        {
            _orchestrator?.Cancel();
            base.OnClose();
        }
    }
}
