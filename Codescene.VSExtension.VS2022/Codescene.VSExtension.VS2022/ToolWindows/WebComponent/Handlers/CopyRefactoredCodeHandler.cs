using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using System.ComponentModel.Composition;
using System.Windows;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(CopyRefactoredCodeHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CopyRefactoredCodeHandler
{

    [Import]
    private readonly ICodeReviewer _reviewer;
    public void CopyToRefactoredCodeToClipboard()
    {
        var cache = _reviewer.GetCachedRefactoredCode();
        Clipboard.SetText(cache.Refactored.Code);
    }
}
