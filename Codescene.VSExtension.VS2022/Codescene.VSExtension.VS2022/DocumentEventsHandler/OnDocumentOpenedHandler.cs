using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnDocumentOpenedHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnDocumentOpenedHandler
{
    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ICliExecuter _cliExecuter;

    public void Handle(string path)
    {
        _logger.Info("Opened document " + (path ?? "no name"));
        _cliExecuter.AddToActiveReviewList(path);
    }
}
