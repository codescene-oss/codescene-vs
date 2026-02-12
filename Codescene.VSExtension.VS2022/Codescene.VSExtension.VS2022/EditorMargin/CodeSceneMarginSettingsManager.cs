// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
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
        ScoreUpdated?.Invoke().FireAndForget();
    }

    public void HideMargin()
    {
        ScoreUpdated?.Invoke().FireAndForget();
    }
}
