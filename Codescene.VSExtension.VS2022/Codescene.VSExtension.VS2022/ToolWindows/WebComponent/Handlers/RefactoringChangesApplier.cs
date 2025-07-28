using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
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
        
        int indentationLevel = 0;
        if (!startsWithSpace)
        {   
            // If it doesn't start with whitespace, we need to determine the indentation level
            indentationLevel = DetectIndentationLevel(snapshot, cache.RefactorableCandidate);
        }
        
        if (indentationLevel > 0)
        {
            newCode = AdjustIndentation(newCode, indentationLevel);
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

    private int DetectIndentationLevel(ITextSnapshot snapshot, FnToRefactorModel refactorableFunction)
    {
        // Get the line at the start of the function
        int startLine = Math.Max(0, refactorableFunction.Range.Startline - 1);
        if (startLine >= snapshot.LineCount)
            return 0;
            
        var line = snapshot.GetLineFromLineNumber(startLine);
        string lineText = line.GetText();
        
        // Count leading spaces
        int leadingSpaces = 0;
        while (leadingSpaces < lineText.Length && char.IsWhiteSpace(lineText[leadingSpaces]))
        {
            leadingSpaces++;
        }
        
        return leadingSpaces ;
    }

    private string AdjustIndentation(string code, int indentationLevel)
    {
        var indentation = new string(' ', indentationLevel); // 4 spaces per level
        var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                lines[i] = indentation + lines[i];
            }
        }
        
        return string.Join(Environment.NewLine, lines);
    }
}
