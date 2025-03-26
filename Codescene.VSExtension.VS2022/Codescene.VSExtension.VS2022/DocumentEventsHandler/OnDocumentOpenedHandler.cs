using Codescene.VSExtension.Core.Application.Services.Cli;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnDocumentOpenedHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnDocumentOpenedHandler
{
    [Import]
    private readonly ICliExecuter _cliExecuter;
    public void Handle(string path)
    {
        VS.StatusBar.ShowMessageAsync("Opened document " + (path ?? "no name")).FireAndForget();
        //var e = _cliExecuter.Review("");
    }
}
