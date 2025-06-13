using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.VS2022.EditorMargin;
using Community.VisualStudio.Toolkit;
using System;
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

    [Import]
    private readonly CodeSceneMarginSettingsManager _marginSettings;

    public void Handle(DocumentView doc)
    {
        _marginSettings.ResetScore();
        var path = doc.Document?.FilePath;
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (_supportedFileChecker.IsNotSupported(extension))
        {
            _logger.Info($"File '{fileName}' is not supported for review.");
            return;
        }

        _reviewer.UseFileOnPathType();
        var review = _reviewer.Review(path, invalidateCache: true);
        _marginSettings.UpdateMarginData(path);
        _errorListWindowHandler.Handle(review);
    }
}
