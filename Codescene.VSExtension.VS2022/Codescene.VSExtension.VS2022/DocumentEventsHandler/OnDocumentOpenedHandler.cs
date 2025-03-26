using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

internal class OnDocumentOpenedHandler
{
    public static void Handle(string obj)
    {
        VS.StatusBar.ShowMessageAsync("Opened document " + (obj ?? "no name")).FireAndForget();
    }
}
