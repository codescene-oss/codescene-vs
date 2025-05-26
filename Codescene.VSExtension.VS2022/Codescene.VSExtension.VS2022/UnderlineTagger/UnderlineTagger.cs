using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.Controls;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
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

            _buffer.Changed += TextBuffer_Changed;

            _timer = new Timer((state) =>
            {
                //On timer tick
                OnTimerElapsed();
            },
            null, TimerInterval, TimerInterval);
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

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            System.Diagnostics.Debug.WriteLine($"Underline positions {_underlinePositions.ToArray()}");

            if (_underlinePositions == null || _underlinePositions.Count == 0)
                yield break;

            var snapshot = _buffer.CurrentSnapshot;

            foreach (var requestSpan in spans)
            {
                foreach (var pos in _underlinePositions)
                {
                    int startLineIdx = pos.StartLine - 1;   // 1‑based ➜ 0‑based
                    int endLineIdx = pos.EndLine   - 1;

                    if (startLineIdx < 0 || endLineIdx < 0 ||
                        startLineIdx >= snapshot.LineCount || endLineIdx >= snapshot.LineCount)
                        continue;

                    var startLine = snapshot.GetLineFromLineNumber(startLineIdx);
                    var endLine = snapshot.GetLineFromLineNumber(endLineIdx);

                    int startCol = Math.Max(0, pos.StartColumn - 1);  // 1‑based ➜ 0‑based
                    int endCol = Math.Max(0, pos.EndColumn);

                    int start = Math.Min(startLine.Start + startCol, startLine.End);
                    int end = Math.Min(endLine.Start   + endCol, endLine.End);

                    if (end <= start)
                        continue;

                    var tagSpan = new SnapshotSpan(snapshot, Span.FromBounds(start, end));

                    if (!tagSpan.IntersectsWith(requestSpan))
                        continue;

                    yield return new TagSpan<IErrorTag>(
                        tagSpan,
                        new ErrorTag(
                            PredefinedErrorTypeNames.Warning,
                            new UnderlineTaggerTooltip(pos.Category, pos.Details)));
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
