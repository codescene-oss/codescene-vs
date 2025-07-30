using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.Controls;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.VS2022.UnderlineTagger
{
    public class ReviewResultTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly string _filePath;
        private readonly ReviewCacheService _cache = new();
        private readonly AceRefactorableFunctionsCacheService _aceRefactorableFunctionsCache = new();

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        internal ReviewResultTagger(ITextBuffer buffer, string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"[TAGGER CREATED] For: {buffer.GetFileName()}");

            _buffer = buffer;
            _filePath = filePath;
        }

        /// <summary>
        /// Called by Visual Studio to retrieve error tags for the currently visible portions of the editor.
        /// 
        /// The 'spans' parameter represents the visible regions of the text buffer (e.g., lines currently on screen).
        /// Visual Studio invokes this method and asks: "For these visible spans, do you have any tags (e.g., underlines) to show?"
        /// 
        /// This method loads any code smells from the cache and maps them to spans in the visible text area,
        /// returning tags only for those that intersect the requested spans.
        /// </summary>
        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var smells = TryLoadFromCache();
            var refactorableFunctions = TryLoadRefactorableFunctionsFromCache();

            if (smells.Count == 0)
                yield break; // No tags, exit early

            FnToRefactorModel? refactorableFunction = null;
            SnapshotSpan? lastTagSpan = null;

            foreach (var visibleSpan in spans)
            {
                foreach (var codeSmell in smells)
                {
                    var tagSpan = TryCreateTagSpan(visibleSpan, codeSmell);

                    if (tagSpan == null)
                        yield break;

                    if (tagSpan.Value.IntersectsWith(visibleSpan))
                    {
                        yield return CreateErrorTagSpan(tagSpan.Value, codeSmell);
                        refactorableFunction = AceUtils.GetRefactorableFunction(codeSmell, refactorableFunctions);
                        if (General.Instance.EnableAutoRefactor && refactorableFunction  is not null)
                        {
                            lastTagSpan = tagSpan;
                        }
                    }
                }
            }
            if (refactorableFunction is not null)
                yield return CreateAceRefactorTagSpan(lastTagSpan.Value, refactorableFunction);
        }

        private List<CodeSmellModel> TryLoadFromCache()
        {
            string currentContent = _buffer.CurrentSnapshot.GetText();
            var cached = _cache.Get(new ReviewCacheQuery(currentContent, _filePath));

            if (cached != null)
                return cached.FileLevel.Concat(cached.FunctionLevel).ToList() ?? [];

            return [];
        }

        private IList<FnToRefactorModel> TryLoadRefactorableFunctionsFromCache()
        {
            var logger = VS.GetMefServiceAsync<ILogger>();
            string currentContent = _buffer.CurrentSnapshot.GetText();
            IList<FnToRefactorModel> cached = _aceRefactorableFunctionsCache.Get(new AceRefactorableFunctionsQuery(_filePath, currentContent));

            return cached;
        }

        public void RefreshTags()
        {
            var snapshot = _buffer.CurrentSnapshot;
            var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        /// <summary>
        /// Attempts to create a <see cref="SnapshotSpan"/> (a range in the text buffer)
        /// that corresponds to the location of the given <paramref name="codeSmell"/>.
        /// 
        /// The span is based on the start and end line/column info from the code smell model.
        /// It ensures that the calculated range is valid and within the current text snapshot.
        /// 
        /// Returns null if:
        /// - The code smell references a line outside the snapshot.
        /// - The calculated span is invalid (e.g., end before start).
        /// - An exception occurs during span creation.
        /// </summary>
        private SnapshotSpan? TryCreateTagSpan(SnapshotSpan requestSpan, CodeSmellModel codeSmell)
        {
            try
            {
                var snapshot = requestSpan.Snapshot;
                var codeSmellStartLine = codeSmell.Range.StartLine - 1;
                var codeSmellEndLine = codeSmell.Range.EndLine - 1;
                var codeSmellStartColumn = codeSmell.Range.StartColumn - 1;
                var codeSmellEndColumn = codeSmell.Range.EndColumn;

                if (codeSmellStartLine < 0 || codeSmellEndLine >= snapshot.LineCount)
                    return null;

                var startLine = snapshot.GetLineFromLineNumber(codeSmellStartLine);
                var endLine = snapshot.GetLineFromLineNumber(codeSmellEndLine);

                // Calculate start position with offset for column (column is 1-based, offset 0-based)
                int startPos = startLine.Start + Math.Max(0, Math.Min(codeSmellStartColumn, startLine.Length));

                // Calculate end position similarly (make sure EndColumn is at least start column)
                int endPos = endLine.Start + Math.Max(0, Math.Min(codeSmellEndColumn, endLine.Length));

                if (endPos <= startPos)
                    return null;

                return new SnapshotSpan(snapshot, Span.FromBounds(startPos, endPos));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a tag span with an error tag and tooltip information.
        /// </summary>
        /// <param name="span">The span to tag.</param>
        /// <param name="pos">The underline position info.</param>
        /// <returns>A <see cref="TagSpan{IErrorTag}"/> instance.</returns>
        private TagSpan<IErrorTag> CreateErrorTagSpan(SnapshotSpan span, CodeSmellModel codeSmell)
        {
            var codeSmellInfo = new UnderlineTaggerTooltipParams(
                codeSmell.Category,
                codeSmell.Details,
                codeSmell.Path,
                codeSmell.Range,
                codeSmell.FunctionName ?? "");

            var errorTag = new ErrorTag(
                PredefinedErrorTypeNames.Warning,
                new UnderlineTaggerTooltip(codeSmellInfo));

            return new TagSpan<IErrorTag>(span, errorTag);
        }

        private TagSpan<IErrorTag> CreateAceRefactorTagSpan(SnapshotSpan span, FnToRefactorModel refactorableFunction)
        {
            var tooltipParams = new AceRefactorTooltipParams(_filePath, refactorableFunction);

            var errorTag = new ErrorTag(
                PredefinedErrorTypeNames.Warning,
                new AceRefactorTooltip(tooltipParams));

            return new TagSpan<IErrorTag>(span, errorTag);
        }
    }
}
