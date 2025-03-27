using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.CodeLens;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnDocumentSavedHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnDocumentSavedHandler
{
    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ICodeReviewer _reviewer;

    public void Handle(string path)
    {
        _logger.Info("Opened document " + (path ?? "no name"));
        //_cliExecuter.RemoveFromActiveReviewList(path);
        //_cliExecuter.AddToActiveReviewList(path);
        CodesceneCodelensCallbackService.RefreshAllCodeLensDataPointsAsync().FireAndForget();
    }
}
