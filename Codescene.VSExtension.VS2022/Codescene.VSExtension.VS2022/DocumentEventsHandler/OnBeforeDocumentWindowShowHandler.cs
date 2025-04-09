using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Community.VisualStudio.Toolkit;
using System.ComponentModel.Composition;
using System.IO;

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

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    public void Handle(DocumentView doc)
    {
        var path = doc.Document?.FilePath;
        _logger.Info(path);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentNullException(nameof(path));
        }

        if (_supportedFileChecker.IsNotSupported(Path.GetExtension(path)))
        {
            return;
        }

        _reviewer.UseFileOnPathType();
        var review = _reviewer.Review(path, invalidateCache: true);
        _errorListWindowHandler.Handle(review);
    }
}
