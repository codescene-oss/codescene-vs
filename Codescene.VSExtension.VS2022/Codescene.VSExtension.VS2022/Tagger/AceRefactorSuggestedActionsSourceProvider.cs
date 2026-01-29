using Codescene.VSExtension.Core.Interfaces.Extension;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.UnderlineTagger;

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("CodeScene ACE Refactor Suggested Actions")]
[ContentType("code")]
internal class AceRefactorSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
{
    [Import]
    internal ISettingsProvider SettingsProvider { get; set; }

    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
    {
        if (textBuffer == null || textView == null)
        {
            return null;
        }

        return new AceRefactorSuggestedActionsSource(this, textView, textBuffer);
    }
}
