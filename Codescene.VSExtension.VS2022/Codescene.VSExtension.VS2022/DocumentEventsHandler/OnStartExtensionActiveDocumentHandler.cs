using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnStartExtensionActiveDocumentHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnStartExtensionActiveDocumentHandler
{
    [Import]
    private readonly ICodeReviewer _reviewer;

    [Import]
    private readonly IErrorListWindowHandler _errorListWindowHandler;

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    [Import]
    private readonly ActiveDocumentTextChangeHandler _documentHandler;

    public void Handle(string path)
    {
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
        //CodesceneCodelensCallbackService.RefreshCodeLensAsync().FireAndForget();

        _ = _documentHandler.SubscribeAsync();
    }
}
