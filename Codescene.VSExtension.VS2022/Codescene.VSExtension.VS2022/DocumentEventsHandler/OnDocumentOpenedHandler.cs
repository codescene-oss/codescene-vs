using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnDocumentOpenedHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnDocumentOpenedHandler
{
    [Import]
    private readonly ICodeReviewer _reviewer;

    public void Handle(string path)
    {
        _reviewer.UseFileOnPathType();
    }
}
