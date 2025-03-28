using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnStartExtensionActiveDocumentHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnStartExtensionActiveDocumentHandler
{

    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ICodeReviewer _reviewer;

    [Import]
    private readonly IErrorListWindowHandler _errorListWindowHandler;

    public void Handle(string path)
    {
        _logger.Info($"Active opened document:{path}");
        var review = _reviewer.Review(path);
        _errorListWindowHandler.Handle(review);
    }
}
