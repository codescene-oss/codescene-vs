using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

[Export(typeof(ShowDiffHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ShowDiffHandler
{
    [Import] private readonly ICodeReviewer _reviewer;

    public async Task ShowDiffWindowAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var cache = _reviewer.GetCachedRefactoredCode();
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

        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var rootDir = Path.GetDirectoryName(assemblyPath);
        var showDiffFolder = Path.Combine(rootDir!, SkipShowDiffHelper.SHOW_DIFF_FOLDER);
        Directory.CreateDirectory(showDiffFolder);

        var extension = Path.GetExtension(cache.Path);
        var leftPath = Path.Combine(showDiffFolder, $"original{extension}");
        var rightPath = Path.Combine(showDiffFolder, $"refactoring{extension}");

        File.WriteAllText(leftPath, original);
        File.WriteAllText(rightPath, refactored);

        await VS.Commands.ExecuteAsync("Tools.DiffFiles", $"\"{leftPath}\" \"{rightPath}\"");
    }
}
