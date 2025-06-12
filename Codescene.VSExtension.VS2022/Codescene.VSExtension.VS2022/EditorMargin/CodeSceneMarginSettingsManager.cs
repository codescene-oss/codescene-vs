using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models.ReviewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.EditorMargin;

[Export(typeof(CodeSceneMarginSettingsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CodeSceneMarginSettingsManager
{
    [Import]
    private readonly IReviewedFilesCacheHandler _cache;

    public event Action ScoreUpdated;
    public bool HasScore { get; private set; } = false;
    public string FileInFocus { get; private set; } = null;

    public void UpdateMarginData(string path)
    {
        FileInFocus = path;
        HasScore = _cache.Exists(path);
        ScoreUpdated?.Invoke();
    }
}

