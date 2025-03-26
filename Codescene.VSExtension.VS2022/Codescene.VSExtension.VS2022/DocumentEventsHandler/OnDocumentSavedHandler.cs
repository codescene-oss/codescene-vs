using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;
internal class OnDocumentSavedHandler
{
    public static void Handle(string obj)
    {
        VS.StatusBar.ShowMessageAsync("Saved document " + (obj ?? "no name")).FireAndForget();
    }
}
