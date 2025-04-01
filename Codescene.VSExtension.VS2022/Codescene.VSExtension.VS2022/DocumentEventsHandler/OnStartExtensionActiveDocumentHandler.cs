using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.VS2022.CodeLens;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.IO;

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

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    public void Handle(string path)
    {
        _logger.Info($"Active opened document:{path}");

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentNullException(nameof(path));
        }

        if (_supportedFileChecker.IsNotSupported(Path.GetExtension(path)))
        {
            return;
        }

        var review = _reviewer.Review(path);
        _errorListWindowHandler.Handle(review);
        CodesceneCodelensCallbackService.RefreshAllCodeLensDataPointsAsync().FireAndForget();
    }
}
