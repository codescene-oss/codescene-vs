using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Community.VisualStudio.Toolkit;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;
[Export(typeof(OnAfterDocumentWindowHideHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnAfterDocumentWindowHideHandler
{
    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ICodeReviewer _reviewer;

    public void Handle(DocumentView doc)
    {
        _logger.Info($"After document window hide:{doc.Document?.FilePath ?? ""}");
        _reviewer.UseFileOnPathType();
    }
}
