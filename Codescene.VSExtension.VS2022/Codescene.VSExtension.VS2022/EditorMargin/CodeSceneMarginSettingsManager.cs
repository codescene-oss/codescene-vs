// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.VS2022.EditorMargin;

[Export(typeof(CodeSceneMarginSettingsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CodeSceneMarginSettingsManager
{
    [Import]
    private IAsyncTaskScheduler _scheduler;

    public event Func<Task> ScoreUpdated;

    public void NotifyScoreUpdated()
    {
        _scheduler.Schedule(ct => InvokeAllSubscribersAsync());
    }

    public void HideMargin()
    {
        _scheduler.Schedule(ct => InvokeAllSubscribersAsync());
    }

    private async Task InvokeAllSubscribersAsync()
    {
        var handler = ScoreUpdated;
        if (handler == null)
        {
            return;
        }

        var tasks = handler.GetInvocationList()
            .Cast<Func<Task>>()
            .Select(subscriber => subscriber())
            .ToArray();

        await Task.WhenAll(tasks);
    }
}
