// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;

namespace Codescene.VSExtension.VS2022.Tagger;

internal interface IAceRefactorSuggestedActionsNotifier
{
    void Register(ITextBuffer buffer, AceRefactorSuggestedActionsSource source);

    void Unregister(ITextBuffer buffer, AceRefactorSuggestedActionsSource source);

    void NotifyAceRefactorableDataChanged(ITextBuffer buffer);
}

[Export(typeof(IAceRefactorSuggestedActionsNotifier))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class AceRefactorSuggestedActionsNotifier : IAceRefactorSuggestedActionsNotifier
{
    private readonly object _lock = new object();
    private readonly Dictionary<ITextBuffer, List<AceRefactorSuggestedActionsSource>> _sourcesByBuffer =
        new Dictionary<ITextBuffer, List<AceRefactorSuggestedActionsSource>>();

    public void Register(ITextBuffer buffer, AceRefactorSuggestedActionsSource source)
    {
        if (buffer == null || source == null)
        {
            return;
        }

        lock (_lock)
        {
            if (!_sourcesByBuffer.TryGetValue(buffer, out var list))
            {
                list = new List<AceRefactorSuggestedActionsSource>();
                _sourcesByBuffer[buffer] = list;
            }

            if (!list.Contains(source))
            {
                list.Add(source);
            }
        }
    }

    public void Unregister(ITextBuffer buffer, AceRefactorSuggestedActionsSource source)
    {
        if (buffer == null || source == null)
        {
            return;
        }

        lock (_lock)
        {
            if (!_sourcesByBuffer.TryGetValue(buffer, out var list))
            {
                return;
            }

            list.Remove(source);
            if (list.Count == 0)
            {
                _sourcesByBuffer.Remove(buffer);
            }
        }
    }

    public void NotifyAceRefactorableDataChanged(ITextBuffer buffer)
    {
        if (buffer == null)
        {
            return;
        }

        List<AceRefactorSuggestedActionsSource> snapshot;
        lock (_lock)
        {
            if (!_sourcesByBuffer.TryGetValue(buffer, out var list) || list.Count == 0)
            {
                return;
            }

            snapshot = list.ToList();
        }

        foreach (var source in snapshot)
        {
            source.RaiseSuggestedActionsChanged();
        }
    }
}
