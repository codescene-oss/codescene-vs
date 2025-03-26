using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnBeforeDocumentWindowShowHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnBeforeDocumentWindowShowHandler
{
    public void Handle(DocumentView doc)
    {
        VS.StatusBar.ShowMessageAsync(doc.Document?.FilePath ?? "").FireAndForget();
    }
}
