using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.UnderlineTagger;

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("CodeScene ACE Refactor Suggested Actions")]
[ContentType("code")]
internal class AceRefactorSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
{
    [Import(typeof(ITextStructureNavigatorSelectorService))]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

    [Import]
    internal ISettingsProvider SettingsProvider { get; set; }

    [Import]
    internal ILogger Logger { get; set; }

    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
    {
        Logger.Debug($"[ACE QuickAction Provider] CreateSuggestedActionsSource called");
        
        if (textBuffer == null || textView == null)
        {
            Logger.Debug($"[ACE QuickAction Provider] textBuffer or textView is null");
            return null;
        }

        Logger.Debug($"[ACE QuickAction Provider] Creating source for: {textBuffer.GetFileName()}");
        return new AceRefactorSuggestedActionsSource(this, Logger, textView, textBuffer);
    }
}
