using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnAfterDocumentWindowHideHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnAfterDocumentWindowHideHandler
{
    public void Handle(DocumentView doc)
    {
        VS.StatusBar.ShowMessageAsync(doc.Document?.FilePath ?? "").FireAndForget();
    }
}
