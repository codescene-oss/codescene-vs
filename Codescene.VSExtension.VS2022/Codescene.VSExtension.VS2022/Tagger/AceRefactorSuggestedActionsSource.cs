using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.Util;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codescene.VSExtension.VS2022.UnderlineTagger;

internal class AceRefactorSuggestedActionsSource : ISuggestedActionsSource
{
    private readonly AceRefactorSuggestedActionsSourceProvider _provider;
    private readonly ITextView _textView;
    private readonly ITextBuffer _textBuffer;
    private readonly ReviewCacheService _reviewCache = new ();
    private readonly AceRefactorableFunctionsCacheService _aceRefactorableFunctionsCache = new ();

    public event EventHandler<EventArgs> SuggestedActionsChanged;

    public AceRefactorSuggestedActionsSource(
        AceRefactorSuggestedActionsSourceProvider provider,
        ITextView textView,
        ITextBuffer textBuffer)
    {
        _provider = provider;
        _textView = textView;
        _textBuffer = textBuffer;
    }

    public Task<bool> HasSuggestedActionsAsync(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            () =>
        {
            if (!HasAuthToken())
            {
                return false;
            }

            var result = TryGetRefactorableFunctionInRange(range);
            return result.HasValue;
        }, cancellationToken);
    }

    public IEnumerable<SuggestedActionSet> GetSuggestedActions(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        if (!HasAuthToken())
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

    private bool HasAuthToken()
    {
        return !string.IsNullOrWhiteSpace(_provider.SettingsProvider?.AuthToken);
    }

    /// <summary>
    /// Tries to find a refactorable function within the given range.
    /// Returns the file path and refactorable function if found, null otherwise.
    /// </summary>
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
            var smells = GetCodeSmellsFromCache(filePath, currentContent);
            var refactorableFunctions = GetRefactorableFunctionsFromCache(filePath, currentContent);

            if (smells == null || refactorableFunctions == null)
            {
                return null;
            }

            var refactorableFunction = FindRefactorableFunctionInRange(range, smells, refactorableFunctions);
            return refactorableFunction != null ? (filePath, refactorableFunction) : null;
        }
        catch
        {
            return null;
        }
    }

    private FnToRefactorModel FindRefactorableFunctionInRange(
        SnapshotSpan range,
        List<CodeSmellModel> smells,
        IList<FnToRefactorModel> refactorableFunctions)
    {
        var rangeStartLine = range.Start.GetContainingLine().LineNumber + 1;
        var rangeEndLine = range.End.GetContainingLine().LineNumber + 1;

        return smells
            .Where(smell => SmellOverlapsRange(smell, rangeStartLine, rangeEndLine))
            .Select(smell => AceUtils.GetRefactorableFunction(smell, refactorableFunctions))
            .FirstOrDefault(fn => fn != null);
    }

    private static bool SmellOverlapsRange(CodeSmellModel smell, int rangeStartLine, int rangeEndLine)
    {
        return smell.Range.StartLine <= rangeEndLine && smell.Range.EndLine >= rangeStartLine;
    }

    private List<CodeSmellModel> GetCodeSmellsFromCache(string filePath, string content)
    {
        var cachedReview = _reviewCache.Get(new ReviewCacheQuery(content, filePath));
        if (cachedReview == null)
        {
            return null;
        }

        var smells = cachedReview.FileLevel.Concat(cachedReview.FunctionLevel).ToList();
        return smells.Count > 0 ? smells : null;
    }

    private IList<FnToRefactorModel> GetRefactorableFunctionsFromCache(string filePath, string content)
    {
        var functions = _aceRefactorableFunctionsCache.Get(new AceRefactorableFunctionsQuery(filePath, content));
        return functions?.Count > 0 ? functions : null;
    }

    public void Dispose()
    {
    }

    public bool TryGetTelemetryId(out Guid telemetryId)
    {
        telemetryId = Guid.Empty;
        return false;
    }
}
