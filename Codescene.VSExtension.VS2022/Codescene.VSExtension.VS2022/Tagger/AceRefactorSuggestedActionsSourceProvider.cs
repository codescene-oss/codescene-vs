// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codescene.VSExtension.VS2022.Tagger;

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("CodeScene Refactoring Agent Suggested Actions")]
[ContentType("code")]
internal class AceRefactorSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
{
    private bool _subscribed;

    [Import]
    internal IAgentAvailability AgentAvailability { get; set; }

    [Import]
    internal IAceRefactorSuggestedActionsNotifier SuggestedActionsNotifier { get; set; }

    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
    {
        if (textBuffer == null || textView == null)
        {
            return null;
        }

        if (!_subscribed)
        {
            AgentAvailability.Changed += OnAgentAvailabilityChanged;
            _subscribed = true;
        }

        return new AceRefactorSuggestedActionsSource(this, textView, textBuffer, SuggestedActionsNotifier);
    }

    private void OnAgentAvailabilityChanged(object sender, EventArgs e)
    {
        SuggestedActionsNotifier.NotifyAll();
    }
}
