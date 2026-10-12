// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

namespace Codescene.VSExtension.VS2022.Application.Services;

[Export(typeof(CodeHealthMonitorBinder))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CodeHealthMonitorBinder
{
    private readonly CodeHealthMonitorNotifier _notifier;
    private readonly IAsyncTaskScheduler _scheduler;

    [ImportingConstructor]
    public CodeHealthMonitorBinder(
        CodeHealthMonitorNotifier notifier,
        IIdeServerClient client,
        IAsyncTaskScheduler scheduler,
        ISettingsProvider settings)
    {
        _notifier = notifier;
        _scheduler = scheduler;
        _notifier.ViewUpdateRequested += OnViewUpdateRequested;
        client.QueueChanged += OnQueueChanged;
        _notifier.SetAutomaticAnalysisEnabled(settings?.EnableAutomaticAnalysis ?? true);
    }

    private void OnQueueChanged(object sender, ReviewQueue queue)
    {
        _notifier.ApplyQueue(queue);
    }

    private void OnViewUpdateRequested(object sender, EventArgs e)
    {
        _scheduler.Schedule(ct => CodeSceneToolWindow.UpdateViewAsync());
    }
}
