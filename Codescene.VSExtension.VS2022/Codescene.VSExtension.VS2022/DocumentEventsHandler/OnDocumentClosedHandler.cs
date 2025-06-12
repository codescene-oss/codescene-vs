using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.EditorMargin;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnDocumentClosedHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnDocumentClosedHandler
{
    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly IReviewedFilesCacheHandler _cache;

    [Import]
    private readonly CodeSceneMarginSettingsManager _marginSettings;

    public void Handle(string path)
    {
        _logger.Info("Closed document " + (path ?? "no name"));
        _cache.Remove(path);
        //_marginSettings.UpdateMarginData(path);
    }
}
