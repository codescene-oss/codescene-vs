using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(RefactoringChangesApplier))]
[PartCreationPolicy(CreationPolicy.Shared)]

public class RefactoringChangesApplier
{
    [Import]
    private readonly IAceManager _aceManager;

    public async Task ApplyAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var cache = _aceManager.GetCachedRefactoredCode();
        var newCode = cache.Refactored.Code;

        var docView = await VS.Documents.OpenAsync(cache.Path);
        if (docView?.TextBuffer is not ITextBuffer buffer)
            return;

        var snapshot = buffer.CurrentSnapshot;

        // Check if newCode already starts with whitespace
        bool startsWithSpace = newCode.Length > 0 && char.IsWhiteSpace(newCode[0]);
        
        IndentationInfo indentationInfo = default;
        if (!startsWithSpace)
        {   
            // If it doesn't start with whitespace, we need to determine the indentation level
            indentationInfo = IndentationUtil.DetectIndentation(snapshot, cache.RefactorableCandidate);
        }
        
        if (indentationInfo.Level > 0)
        {
            newCode = IndentationUtil.AdjustIndentation(newCode, indentationInfo);
        }

        int start = Math.Max(1, cache.RefactorableCandidate.Range.Startline) - 1;
        int end = Math.Max(1, cache.RefactorableCandidate.Range.EndLine)   - 1;

        if (start >= snapshot.LineCount)
            return;

        end = Math.Min(end, snapshot.LineCount - 1);

        var startLine = snapshot.GetLineFromLineNumber(start);
        var endLine = snapshot.GetLineFromLineNumber(end);

        var span = new Span(
            startLine.Start.Position,
            endLine.EndIncludingLineBreak.Position - startLine.Start.Position);

        using var edit = buffer.CreateEdit();
        edit.Replace(span, newCode.EndsWith("\r\n") ? newCode : newCode + Environment.NewLine);
        edit.Apply();

        var postSnapshot = buffer.CurrentSnapshot;

        var selectionSpan = new SnapshotSpan(
            postSnapshot,
            new Span(startLine.Start.Position, newCode.Length));

        var view = docView.TextView;
        view.Selection.Select(selectionSpan, isReversed: false);
        view.Caret.MoveTo(selectionSpan.Start);
        view.ViewScroller.EnsureSpanVisible(selectionSpan);
    }
}
