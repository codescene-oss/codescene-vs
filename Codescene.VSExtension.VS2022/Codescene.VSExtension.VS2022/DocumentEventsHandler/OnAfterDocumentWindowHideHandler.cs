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

    public void Handle(DocumentView doc)
    {
        _logger.Info(doc.Document?.FilePath ?? "");
    }
}
