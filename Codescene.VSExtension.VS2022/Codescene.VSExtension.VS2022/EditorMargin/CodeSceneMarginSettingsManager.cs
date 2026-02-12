// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.EditorMargin;

[Export(typeof(CodeSceneMarginSettingsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CodeSceneMarginSettingsManager
{
    public event Func<Task> ScoreUpdated;

    public void NotifyScoreUpdated()
    {
        InvokeAllSubscribersAsync().FireAndForget();
    }

    public void HideMargin()
    {
        InvokeAllSubscribersAsync().FireAndForget();
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
