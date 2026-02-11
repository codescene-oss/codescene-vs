// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.EditorMargin;

[Export(typeof(CodeSceneMarginSettingsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CodeSceneMarginSettingsManager
{
    public event Func<Task> ScoreUpdated;

    public bool HasScore { get; private set; }

    public bool HasDelta { get; private set; }

    public string FileInFocus { get; private set; }

    public string FileInFocusContent { get; private set; }

    public void UpdateMarginData(string path, string content)
    {
        FileInFocus = path;
        FileInFocusContent = content;

        var deltaCache = new DeltaCacheService();
        var delta = deltaCache.GetDeltaForFile(path);

        if (delta != null)
        {
            HasDelta = true;
            HasScore = true;
        }
        else
        {
            var cache = new ReviewCacheService();
            var cacheItem = cache.Get(new ReviewCacheQuery(content, path));
            HasScore = cacheItem != null;
            HasDelta = false;
        }

        ScoreUpdated?.Invoke().FireAndForget();
    }

    public void HideMargin()
    {
        HasScore = false;
        ScoreUpdated?.Invoke().FireAndForget();
    }
}
