using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

[Export(typeof(RefactoringChangesApplier))]
[PartCreationPolicy(CreationPolicy.Shared)]

public class RefactoringChangesApplier
{

    [Import]
    private readonly ICodeReviewer _reviewer;

    public async Task ApplyAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var cache = _reviewer.GetCachedRefactoredCode();
        var newCode = cache.Refactored.Code;

        var docView = await VS.Documents.OpenAsync(cache.Path);
        if (docView?.TextBuffer is not ITextBuffer buffer)
            return;

        var snapshot = buffer.CurrentSnapshot;

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
    }
}
