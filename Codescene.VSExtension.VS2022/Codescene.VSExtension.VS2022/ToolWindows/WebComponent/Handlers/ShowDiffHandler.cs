using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

[Export(typeof(ShowDiffHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ShowDiffHandler
{
    [Import] private readonly IAceManager _aceManager;

    public async Task ShowDiffWindowAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var cache = _aceManager.GetCachedRefactoredCode();
        var newCode = cache.Refactored.Code;
        var docView = await VS.Documents.OpenAsync(cache.Path);
        if (docView?.TextBuffer is not ITextBuffer buffer)
            return;

        var snapshot = buffer.CurrentSnapshot;
        var start = Math.Max(1, cache.RefactorableCandidate.Range.Startline) - 1;
        var end = Math.Min(Math.Max(1, cache.RefactorableCandidate.Range.EndLine) - 1,
                            snapshot.LineCount - 1);
        if (start >= snapshot.LineCount) return;

        var span = new Span(
            snapshot.GetLineFromLineNumber(start).Start.Position,
            snapshot.GetLineFromLineNumber(end).EndIncludingLineBreak.Position
            - snapshot.GetLineFromLineNumber(start).Start.Position
        );

        var original = snapshot.GetText();
        var replacement = newCode.EndsWith(Environment.NewLine) ? newCode : newCode + Environment.NewLine;
        var refactored = original.Remove(span.Start, span.Length).Insert(span.Start, replacement);

        // Write the original and refactored code to temporary files
        var tempOriginalPath = Path.GetTempFileName();
        var tempRefactoredPath = Path.GetTempFileName();
        File.WriteAllText(tempOriginalPath, original);
        File.WriteAllText(tempRefactoredPath, refactored);

        //await VS.Commands.ExecuteAsync("Tools.DiffFiles", $"\"{tempOriginalPath}\" \"{tempRefactoredPath}\"");

        // Use VS.GetServiceAsync to get the difference service
        var diffService = await VS.GetServiceAsync<SVsDifferenceService, IVsDifferenceService>();

        // Open the diff window with the temporary files
        diffService.OpenComparisonWindow2(
            tempOriginalPath,
            tempRefactoredPath,
            "Code Comparison",
            null,
            Path.GetFileName(cache.Path) + " (Original)",
            Path.GetFileName(cache.Path) + " (Refactored)",
            null,
            null,
            0);

        // Optionally, schedule deletion of temp files after some delay or on process exit
        // File.Delete(tempOriginalPath);
        // File.Delete(tempRefactoredPath);
    }
}
