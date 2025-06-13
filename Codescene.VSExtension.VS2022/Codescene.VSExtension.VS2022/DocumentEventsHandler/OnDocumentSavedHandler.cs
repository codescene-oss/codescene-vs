using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.VS2022.EditorMargin;
using System;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnDocumentSavedHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnDocumentSavedHandler
{
    [Import]
    private readonly ICodeReviewer _reviewer;

    [Import]
    private readonly IErrorListWindowHandler _errorListWindowHandler;

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    [Import]
    private readonly CodeSceneMarginSettingsManager _marginSettings;

    public void Handle(string path)
    {
        _marginSettings.ResetScore();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (_supportedFileChecker.IsNotSupported(Path.GetExtension(path)))
        {
            return;
        }

        _reviewer.UseFileOnPathType();
        var review = _reviewer.Review(path);
        _marginSettings.UpdateMarginData(path);
        _errorListWindowHandler.Handle(review);
        //CodesceneCodelensCallbackService.RefreshCodeLensAsync().FireAndForget();
    }
}
