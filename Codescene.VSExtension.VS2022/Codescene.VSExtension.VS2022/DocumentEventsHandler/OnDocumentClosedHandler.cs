using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnDocumentClosedHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnDocumentClosedHandler
{
    public void Handle(string path)
    {
        VS.StatusBar.ShowMessageAsync("Closed document " + (path ?? "no name")).FireAndForget();
    }
}
