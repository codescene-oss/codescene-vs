// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.VS2022.Controls;
using Codescene.VSExtension.VS2022.Tagger;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codescene.VSExtension.VS2022.UnderlineTagger
{
    public class ReviewResultTagger : ITagger<IErrorTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly string _filePath;
        private readonly ReviewCacheService _cache = new ReviewCacheService();
        private bool _disposed;

        internal ReviewResultTagger(ITextBuffer buffer, string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"[TAGGER CREATED] For: {buffer.GetFileName()}");

            _buffer = buffer;
            _filePath = filePath;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

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

            if (smells.Count == 0)
            {
                yield break; // No tags, exit early
            }

            foreach (var visibleSpan in spans)
            {
                foreach (var codeSmell in smells)
                {
                    var tagSpan = TryCreateTagSpan(new TagSpanParams(visibleSpan, codeSmell));
                    if (tagSpan != null && tagSpan.Value.IntersectsWith(visibleSpan))
                    {
                        yield return CreateErrorTagSpan(new TagSpanParams(tagSpan.Value, codeSmell));
                    }
                }
            }
        }

        public void RefreshTags()
        {
            var snapshot = _buffer.CurrentSnapshot;
            var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Reserved for future cleanup
            }

            _disposed = true;
        }

        private List<CodeSmellModel> TryLoadFromCache()
        {
            string currentContent = _buffer.CurrentSnapshot.GetText();
            var cached = _cache.Get(new ReviewCacheQuery(currentContent, _filePath));

            if (cached != null)
            {
                return cached.FileLevel.Concat(cached.FunctionLevel).ToList() ?? new List<CodeSmellModel>();
            }

            return new List<CodeSmellModel>();
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
        private SnapshotSpan? TryCreateTagSpan(TagSpanParams tagSpanParams)
        {
            try
            {
                var snapshot = tagSpanParams.Span.Snapshot;
                var codeSmellStartLine = tagSpanParams.CodeSmell.Range.StartLine - 1;
                var codeSmellEndLine = tagSpanParams.CodeSmell.Range.EndLine - 1;
                var codeSmellStartColumn = tagSpanParams.CodeSmell.Range.StartColumn - 1;
                var codeSmellEndColumn = tagSpanParams.CodeSmell.Range.EndColumn;

                if (codeSmellStartLine < 0 || codeSmellEndLine >= snapshot.LineCount)
                {
                    return null;
                }

                var startLine = snapshot.GetLineFromLineNumber(codeSmellStartLine);
                var endLine = snapshot.GetLineFromLineNumber(codeSmellEndLine);

                // Calculate start position with offset for column (column is 1-based, offset 0-based)
                int startPos = startLine.Start + Math.Max(0, Math.Min(codeSmellStartColumn, startLine.Length));

                // Calculate end position similarly (make sure EndColumn is at least start column)
                int endPos = endLine.Start + Math.Max(0, Math.Min(codeSmellEndColumn, endLine.Length));

                if (endPos <= startPos)
                {
                    return null;
                }

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
        /// <returns>A <see cref="TagSpan{IErrorTag}"/> instance.</returns>
        private TagSpan<IErrorTag> CreateErrorTagSpan(TagSpanParams tagSpanParams)
        {
            var codeSmellInfo = new UnderlineTaggerTooltipParams(
                tagSpanParams.CodeSmell.Category,
                tagSpanParams.CodeSmell.Details,
                tagSpanParams.CodeSmell.Path,
                tagSpanParams.CodeSmell.Range,
                tagSpanParams.CodeSmell.FunctionName ?? string.Empty,
                tagSpanParams.CodeSmell.FunctionRange);

            var errorTag = new ErrorTag(
                PredefinedErrorTypeNames.Warning,
                new UnderlineTaggerTooltip(codeSmellInfo));

            return new TagSpan<IErrorTag>(tagSpanParams.Span, errorTag);
        }

        internal class TagSpanParams(SnapshotSpan span, CodeSmellModel codeSmell)
        {
            public SnapshotSpan Span { get; set; } = span;

            public CodeSmellModel CodeSmell { get; set; } = codeSmell;
        }
    }
}
