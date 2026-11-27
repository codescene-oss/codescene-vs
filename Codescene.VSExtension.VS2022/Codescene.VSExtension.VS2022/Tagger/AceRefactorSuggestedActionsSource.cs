// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.Util;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codescene.VSExtension.VS2022.Tagger;

internal class AceRefactorSuggestedActionsSource : ISuggestedActionsSource
{
    private readonly AceRefactorSuggestedActionsSourceProvider _provider;
    private readonly ITextView _textView;
    private readonly ITextBuffer _textBuffer;
    private readonly IAceRefactorSuggestedActionsNotifier _notifier;
    private readonly ReviewCacheService _reviewCache = new ReviewCacheService();

    public AceRefactorSuggestedActionsSource(
        AceRefactorSuggestedActionsSourceProvider provider,
        ITextView textView,
        ITextBuffer textBuffer,
        IAceRefactorSuggestedActionsNotifier notifier)
    {
        _provider = provider;
        _textView = textView;
        _textBuffer = textBuffer;
        _notifier = notifier;
        _notifier?.Register(textBuffer, this);
    }

    public event EventHandler<EventArgs> SuggestedActionsChanged;

    public Task<bool> HasSuggestedActionsAsync(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_provider.AgentAvailability.Usable)
        {
            return Task.FromResult(false);
        }

        var result = TryGetRefactorableFunctionInRange(range);
        return Task.FromResult(result.HasValue);
    }

    public IEnumerable<SuggestedActionSet> GetSuggestedActions(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        if (!_provider.AgentAvailability.Usable)
        {
            return Enumerable.Empty<SuggestedActionSet>();
        }

        var result = TryGetRefactorableFunctionInRange(range);
        if (!result.HasValue)
        {
            return Enumerable.Empty<SuggestedActionSet>();
        }

        var (filePath, refactorableFunction) = result.Value;
        var action = new AceRefactorSuggestedAction(filePath, refactorableFunction);

        return new SuggestedActionSet[]
        {
            new SuggestedActionSet(
                categoryName: PredefinedSuggestedActionCategoryNames.Refactoring,
                actions: new ISuggestedAction[] { action },
                title: "CodeScene",
                priority: SuggestedActionSetPriority.Medium),
        };
    }

    public void Dispose()
    {
        _notifier?.Unregister(_textBuffer, this);
    }

    public bool TryGetTelemetryId(out Guid telemetryId)
    {
        telemetryId = Guid.Empty;
        return false;
    }

    internal void RaiseSuggestedActionsChanged()
    {
        SuggestedActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private (string FilePath, FnToRefactorModel RefactorableFunction)? TryGetRefactorableFunctionInRange(SnapshotSpan range)
    {
        try
        {
            var filePath = _textBuffer.GetFileName();
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            var currentContent = _textBuffer.CurrentSnapshot.GetText();
            var cachedReview = _reviewCache.Get(new ReviewCacheQuery(currentContent, filePath));
            if (cachedReview == null)
            {
                return null;
            }

            var smells = cachedReview.FileLevel.Concat(cachedReview.FunctionLevel).ToList();
            if (smells.Count == 0)
            {
                return null;
            }

            var rangeStartLine = range.Start.GetContainingLine().LineNumber + 1;
            var rangeEndLine = range.End.GetContainingLine().LineNumber + 1;

            var smell = CodeSmellLookup.FirstOverlapping(smells, rangeStartLine, rangeEndLine);
            if (smell == null)
            {
                return null;
            }

            var function = FunctionToRefactorResolver.ForCodeSmell(filePath, currentContent, smell);
            return function != null ? (filePath, function) : null;
        }
        catch
        {
            return null;
        }
    }
}
