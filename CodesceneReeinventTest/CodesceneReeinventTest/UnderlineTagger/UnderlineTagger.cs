using CodesceneReeinventTest.Controls;
using Core.Models;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodesceneReeinventTest.ErrorList
{
    public class UnderlineTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private List<ReviewModel> _underlinePositions;
        private readonly Func<Task<List<ReviewModel>>> _refreshUnderlinePositions;

        public UnderlineTagger(ITextBuffer buffer, List<ReviewModel> underlinePositions, Func<Task<List<ReviewModel>>> refreshUnderlinePositions)
        {
            _buffer = buffer;
            _underlinePositions = underlinePositions;
            _refreshUnderlinePositions = refreshUnderlinePositions;
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
                        yield return new TagSpan<IErrorTag>(lineSpan,
                            new ErrorTag
                            (
                                PredefinedErrorTypeNames.Warning,
                                new UnderlineTaggerTooltip(position.Category, position.Details)
                            )
                        );
                    }
                }
            }
        }
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        private async void OnBufferChanged(TextContentChangedEventArgs args)
        {
            _underlinePositions = await _refreshUnderlinePositions.Invoke();
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(args.After, new Span())));
        }
    }
}
