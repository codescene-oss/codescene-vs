using Codescene.VSExtension.Core.Application.Services.AceManager;
using System.ComponentModel.Composition;
using System.Windows;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(CopyRefactoredCodeHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CopyRefactoredCodeHandler
{

    [Import]
    private readonly IAceManager _aceManager;
    public void CopyToRefactoredCodeToClipboard()
    {
        var cache = _aceManager.GetCachedRefactoredCode();
        Clipboard.SetText(cache.Refactored.Code);
    }
}
