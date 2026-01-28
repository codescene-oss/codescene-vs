using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.Util;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.UnderlineTagger;

internal class AceRefactorSuggestedActionsSource : ISuggestedActionsSource
{
    private readonly AceRefactorSuggestedActionsSourceProvider _provider;
    private readonly ILogger _logger;
    private readonly ITextView _textView;
    private readonly ITextBuffer _textBuffer;
    private readonly ReviewCacheService _reviewCache = new();
    private readonly AceRefactorableFunctionsCacheService _aceRefactorableFunctionsCache = new();

    public event EventHandler<EventArgs> SuggestedActionsChanged;

    public AceRefactorSuggestedActionsSource(
        AceRefactorSuggestedActionsSourceProvider provider,
        ILogger logger,
        ITextView textView,
        ITextBuffer textBuffer)
    {
        _provider = provider;
        _logger = logger;
        _textView = textView;
        _textBuffer = textBuffer;
    }

    public Task<bool> HasSuggestedActionsAsync(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(() =>
        {
            var hasToken = HasAuthToken();
            _logger.Debug($"[ACE QuickAction] HasAuthToken: {hasToken}");
            
            if (!hasToken)
                return false;

            var result = TryGetRefactorableFunctionInRange(range);
            _logger.Debug($"[ACE QuickAction] HasRefactorableFunction: {result.HasValue}");
            return result.HasValue;
        }, cancellationToken);
    }

    public IEnumerable<SuggestedActionSet> GetSuggestedActions(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        if (!HasAuthToken())
            return Enumerable.Empty<SuggestedActionSet>();

        var result = TryGetRefactorableFunctionInRange(range);
        if (!result.HasValue)
            return Enumerable.Empty<SuggestedActionSet>();

        var (filePath, refactorableFunction) = result.Value;
        var action = new AceRefactorSuggestedAction(filePath, refactorableFunction);

        return new SuggestedActionSet[]
        {
            new SuggestedActionSet(
                categoryName: PredefinedSuggestedActionCategoryNames.Refactoring,
                actions: new ISuggestedAction[] { action },
                title: "CodeScene",
                priority: SuggestedActionSetPriority.Medium)
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
            _logger.Debug($"[ACE QuickAction] FilePath: {filePath}");
            if (string.IsNullOrEmpty(filePath))
                return null;

            var currentContent = _textBuffer.CurrentSnapshot.GetText();
            var smells = GetCodeSmellsFromCache(filePath, currentContent);
            var refactorableFunctions = GetRefactorableFunctionsFromCache(filePath, currentContent);

            _logger.Debug($"[ACE QuickAction] Smells: {smells?.Count ?? 0}, RefactorableFunctions: {refactorableFunctions?.Count ?? 0}");

            if (smells == null || refactorableFunctions == null)
                return null;

            // Get line numbers from the range (convert to 1-based)
            var startLine = range.Start.GetContainingLine().LineNumber + 1;
            var endLine = range.End.GetContainingLine().LineNumber + 1;
            _logger.Debug($"[ACE QuickAction] Range: {startLine}-{endLine}");

            return FindRefactorableFunctionInRange(filePath, smells, refactorableFunctions, startLine, endLine);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[ACE QuickAction] Error: {ex.Message}");
            return null;
        }
    }

    private List<CodeSmellModel> GetCodeSmellsFromCache(string filePath, string content)
    {
        var cachedReview = _reviewCache.Get(new ReviewCacheQuery(content, filePath));
        if (cachedReview == null)
            return null;

        var smells = cachedReview.FileLevel.Concat(cachedReview.FunctionLevel).ToList();
        return smells.Count > 0 ? smells : null;
    }

    private IList<FnToRefactorModel> GetRefactorableFunctionsFromCache(string filePath, string content)
    {
        var functions = _aceRefactorableFunctionsCache.Get(new AceRefactorableFunctionsQuery(filePath, content));
        return functions?.Count > 0 ? functions : null;
    }

    private static (string, FnToRefactorModel)? FindRefactorableFunctionInRange(
        string filePath,
        List<CodeSmellModel> smells,
        IList<FnToRefactorModel> refactorableFunctions,
        int rangeStartLine,
        int rangeEndLine)
    {
        foreach (var smell in smells)
        {
            // Check if the smell overlaps with the range
            bool overlaps = smell.Range.StartLine <= rangeEndLine && smell.Range.EndLine >= rangeStartLine;
            if (!overlaps)
                continue;

            var refactorableFunction = AceUtils.GetRefactorableFunction(smell, refactorableFunctions);
            if (refactorableFunction != null)
                return (filePath, refactorableFunction);
        }
        return null;
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
