using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;
public class OnDocumentClosedHandler
{
    public static void Handle(string obj)
    {
        VS.StatusBar.ShowMessageAsync("Closed document " + (obj ?? "no name")).FireAndForget();
    }
}
