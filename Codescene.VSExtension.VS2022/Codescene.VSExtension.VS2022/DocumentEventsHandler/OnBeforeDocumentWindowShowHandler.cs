using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Community.VisualStudio.Toolkit;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnBeforeDocumentWindowShowHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnBeforeDocumentWindowShowHandler
{
    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ICodeReviewer _reviewer;

    [Import]
    private readonly IErrorListWindowHandler _errorListWindowHandler;

    public void Handle(DocumentView doc)
    {
        _logger.Info(doc.Document?.FilePath ?? "");
        var review = _reviewer.Review(doc.Document.FilePath);
        _errorListWindowHandler.Handle(review);
    }
}
