using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.Controls;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Codescene.VSExtension.VS2022.ErrorList
{
    public class UnderlineTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private List<CodeSmellModel> _underlinePositions;
        private readonly Func<List<CodeSmellModel>> _refreshUnderlinePositions;
        private readonly Timer _timer;
        private volatile bool _changed;

        TimeSpan TimerInterval { get { return TimeSpan.FromMilliseconds(Constants.Utils.TEXT_CHANGE_CHECK_INTERVAL_MILISECONDS); } }

        public UnderlineTagger(ITextBuffer buffer, List<CodeSmellModel> underlinePositions, Func<List<CodeSmellModel>> refreshUnderlinePositions)
        {
            _buffer = buffer;
            _underlinePositions = underlinePositions;
            _refreshUnderlinePositions = refreshUnderlinePositions;

            //_buffer.Changed += TextBuffer_Changed;

            //_timer = new Timer((state) =>
            //{
            //    //On timer tick
            //    OnTimerElapsed();
            //},
            //null, TimerInterval, TimerInterval);
        }

        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            _changed = true;
        }

        private void OnTimerElapsed()
        {
            if (_changed)
            {
                _changed = false;
                _underlinePositions = _refreshUnderlinePositions.Invoke();
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, new Span())));
            }
        }

        /// <summary>
        /// Returns tags to underline code issues within the given spans.
        /// </summary>
        /// <param name="spans">The spans requested to be tagged.</param>
        /// <returns>An enumerable of <see cref="ITagSpan{IErrorTag}"/> representing underlined ranges.</returns>
        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            yield break;
            //bool shouldSkipTagging = _underlinePositions == null || _underlinePositions.Count == 0 || spans.Count == 0;
            //if (shouldSkipTagging)
            //    yield break;

            //var snapshot = _buffer.CurrentSnapshot;

            //foreach (var requestSpan in spans)
            //{
            //    foreach (var tagSpan in GetIntersectingTagSpans(snapshot, requestSpan))
            //        yield return tagSpan;
            //}
        }

        private IEnumerable<ITagSpan<IErrorTag>> GetIntersectingTagSpans(ITextSnapshot snapshot, SnapshotSpan requestSpan)
        {
            foreach (var position in _underlinePositions)
            {
                var tagSpan = TryCreateTagSpan(snapshot, position);

                if (tagSpan.HasValue && tagSpan.Value.IntersectsWith(requestSpan))
                    yield return CreateErrorTagSpan(tagSpan.Value, position);
            }
        }

        /// <summary>
        /// Creates a <see cref="SnapshotSpan"/> for the underline position, applying special logic for the first line.
        /// Returns null if the position is invalid or out of range.
        /// </summary>
        /// <param name="snapshot">The current text snapshot.</param>
        /// <param name="pos">The underline position details.</param>
        /// <returns>A <see cref="SnapshotSpan"/> if valid; otherwise null.</returns>
        private SnapshotSpan? TryCreateTagSpan(ITextSnapshot snapshot, CodeSmellModel codeSmell)
        {
            if (!TryGetLines(snapshot, codeSmell, out var startLine, out var endLine))
                return null;

            var spanStart = CalculateSpanStart(startLine, codeSmell.Range.StartColumn);
            var spanEnd = codeSmell.Range.StartLine == 1
                ? GetEndOfFirstNonEmptyLine(snapshot, startLine.LineNumber)
                : CalculateSpanEnd(endLine, codeSmell.Range.EndColumn);

            if (spanEnd <= spanStart)
                return null;

            spanStart = Math.Min(spanStart, startLine.End);
            spanEnd = Math.Min(spanEnd, endLine.End);

            try
            {
                return new SnapshotSpan(snapshot, Span.FromBounds(spanStart, spanEnd));
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
            var errorTag = new ErrorTag(
                PredefinedErrorTypeNames.Warning,
                new UnderlineTaggerTooltip(
                    new UnderlineTaggerTooltipParams(
                        codeSmell.Category,
                        codeSmell.Details,
                        codeSmell.Path,
                        codeSmell.Range,
                        codeSmell.FunctionName ?? ""
                        )
                    )
                );

            return new TagSpan<IErrorTag>(span, errorTag);
        }

        private bool TryGetLines(ITextSnapshot snapshot, CodeSmellModel codeSmell, out ITextSnapshotLine startLine, out ITextSnapshotLine endLine)
        {
            startLine = null;
            endLine = null;

            int startLineIndex = codeSmell.Range.StartLine - 1;
            int endLineIndex = codeSmell.Range.EndLine - 1;

            bool shouldGetLines = startLineIndex < 0 || endLineIndex < 0 || startLineIndex >= snapshot.LineCount || endLineIndex >= snapshot.LineCount;
            if (shouldGetLines)
                return false;

            startLine = snapshot.GetLineFromLineNumber(startLineIndex);
            endLine = snapshot.GetLineFromLineNumber(endLineIndex);
            return true;
        }

        private int CalculateSpanStart(ITextSnapshotLine startLine, int startColumn)
        {
            int lineLength = startLine.Length;
            int columnOffset = Math.Max(0, Math.Min(startColumn - 1, lineLength));
            return startLine.Start + columnOffset;
        }

        private int CalculateSpanEnd(ITextSnapshotLine endLine, int endColumn)
        {
            int lineLength = endLine.Length;
            int columnOffset = Math.Max(0, Math.Min(endColumn, lineLength));
            return endLine.Start + columnOffset;
        }

        /// <summary>
        /// Finds the position just after the last non-whitespace character 
        /// on the first non-empty line starting from <paramref name="startLineIndex"/>.
        /// If no non-empty line is found, returns the start of the original line.
        /// </summary>
        private int GetEndOfFirstNonEmptyLine(ITextSnapshot snapshot, int startLineIndex)
        {
            foreach (var line in snapshot.Lines.Skip(startLineIndex))
            {
                string text = line.GetText();

                int lastNonWhitespaceIndex = text.Length - 1;
                while (lastNonWhitespaceIndex >= 0 && char.IsWhiteSpace(text[lastNonWhitespaceIndex]))
                    lastNonWhitespaceIndex--;

                if (lastNonWhitespaceIndex >= 0)
                    return line.Start + lastNonWhitespaceIndex + 1;
            }

            return snapshot.GetLineFromLineNumber(startLineIndex).Start;
        }


        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
