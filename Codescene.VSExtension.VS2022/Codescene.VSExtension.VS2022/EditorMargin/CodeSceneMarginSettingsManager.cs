using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.EditorMargin;

[Export(typeof(CodeSceneMarginSettingsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CodeSceneMarginSettingsManager
{
    public event Action ScoreUpdated;
    public bool HasScore { get; private set; } = false;
    public string FileInFocus { get; private set; } = null;
    public string FileInFocusContent { get; private set; } = null;

    public void UpdateMarginData(string path, string content)
    {
        var cache = new ReviewCacheService();
        var cacheItem = cache.Get(new ReviewCacheQuery(content, path));

        FileInFocus = path;
        FileInFocusContent = content;
        HasScore = cacheItem != null;
        ScoreUpdated?.Invoke();
    }

    public void HideMargin()
    {
        HasScore = false;
        ScoreUpdated?.Invoke();
    }
}

