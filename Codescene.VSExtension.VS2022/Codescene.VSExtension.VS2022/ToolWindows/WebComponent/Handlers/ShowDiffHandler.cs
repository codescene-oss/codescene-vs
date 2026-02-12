// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(ShowDiffHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ShowDiffHandler
{
    [Import]
    private readonly IAceManager _aceManager;

    public async Task ShowDiffWindowAsync()
    {
        var cache = _aceManager.GetCachedRefactoredCode();
        var newCode = cache.Refactored.Code;

        var normalizedNewCode = TextUtils.NormalizeLineEndings(newCode);
        var tempOriginalPath = Path.GetTempFileName();
        var tempRefactoredPath = Path.GetTempFileName();

        // Switch to UI thread only for Visual Studio API calls
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var docView = await VS.Documents.OpenAsync(cache.Path);
        if (docView?.TextBuffer is not ITextBuffer buffer)
        {
            return;
        }

        var snapshot = buffer.CurrentSnapshot;
        var start = Math.Max(1, cache.RefactorableCandidate.Range.StartLine) - 1;
        var end = Math.Min(
            Math.Max(1, cache.RefactorableCandidate.Range.EndLine) - 1,
            snapshot.LineCount - 1);
        if (start >= snapshot.LineCount)
        {
            return;
        }

        var span = new Span(
            snapshot.GetLineFromLineNumber(start).Start.Position, snapshot.GetLineFromLineNumber(end).EndIncludingLineBreak.Position - snapshot.GetLineFromLineNumber(start).Start.Position);

        var original = snapshot.GetText();
        var refactored = original.Remove(span.Start, span.Length).Insert(span.Start, normalizedNewCode);

        // Write files (could be moved to background thread if desired, but it's fast)
        File.WriteAllText(tempOriginalPath, original);
        File.WriteAllText(tempRefactoredPath, refactored);

        var diffService = await VS.GetServiceAsync<SVsDifferenceService, IVsDifferenceService>();

        // Open the diff window (must be on UI thread)
        diffService?.OpenComparisonWindow2(
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
