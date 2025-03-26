using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;
internal class OnDocumentWindowHideHandler
{
    public static void Handle(DocumentView obj)
    {
        VS.StatusBar.ShowMessageAsync(obj.Document?.FilePath ?? "").FireAndForget();
    }
}
