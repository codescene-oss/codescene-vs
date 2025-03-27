using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
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

    public void Handle(string path)
    {
        _logger.Info("Closed document " + (path ?? "no name"));
        _cache.Remove(path);
    }
}
