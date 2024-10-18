using Core.Models;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.Generic;

namespace CodesceneReeinventTest.ErrorList
{
    public class UnderlineTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly List<ReviewModel> _underlinePositions;

        public UnderlineTagger(ITextBuffer buffer, List<ReviewModel> underlinePositions)
        {
            _buffer = buffer;
            _underlinePositions = underlinePositions;

            _buffer.Changed += (sender, args) => OnBufferChanged(args);
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var span in spans)
            {
                foreach (var position in _underlinePositions)
                {
                    var startLine = _buffer.CurrentSnapshot.GetLineFromLineNumber(position.StartLine);
                    var endLine = _buffer.CurrentSnapshot.GetLineFromLineNumber(position.EndLine);

                    var start = startLine.Start + position.StartColumn;
                    var end = endLine.Start + position.EndColumn;

                    if (start < startLine.End && end <= endLine.End)
                    {
                        var lineSpan = new SnapshotSpan(start, end - start);
                        yield return new TagSpan<IErrorTag>(lineSpan, new ErrorTag(PredefinedErrorTypeNames.Warning, position.Category + " (" + position.Details + ")"));
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        private void OnBufferChanged(TextContentChangedEventArgs args)
        {
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(args.After, new Span())));
        }
    }
}
