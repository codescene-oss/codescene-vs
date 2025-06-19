using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorListWindowHandler;

[Export(typeof(IErrorListWindowHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class ErrorListWindowHandler : IErrorListWindowHandler
{
    private readonly ErrorListProvider _errorListProvider = new(VS2022Package.Instance);

    private void Add(IEnumerable<CodeSmellModel> issues)
    {
        foreach (var issue in issues)
        {
            Add(issue);
        }
    }

    private string FormatMessage(CodeSmellModel codeSmell, bool includeDetails = true)
    {
        string title = $"{Titles.CODESCENE} - {codeSmell.Category}";

        if (includeDetails && !string.IsNullOrEmpty(codeSmell.Details))
        {
            title += $" ({codeSmell.Details})";
        }

        return title;
    }

    private void Add(CodeSmellModel issue)
    {
        var errorTask = new ErrorTask
        {
            ErrorCategory = TaskErrorCategory.Warning,
            Category = TaskCategory.CodeSense,
            Text = FormatMessage(issue),
            Document = issue.Path,
            Line = issue.Range.StartLine - 1, //0-based field
            Column = issue.Range.StartColumn - 1, //0-based field
            HierarchyItem = HierarchyHelper.GetHierarchyFromFile(VS2022Package.Instance, issue.Path),
            SubcategoryIndex = 2,
            HelpKeyword = FormatMessage(issue, false)
        };

        errorTask.Navigate += (sender, e) => { OpenDocumentWithIssue(sender, e, issue.Path); };
        _errorListProvider.Tasks.Add(errorTask);
    }

    private void OpenDocumentWithIssue(object sender, EventArgs e, string path)
    {
        var task = sender as ErrorTask;
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.OpenDocument(VS2022Package.Instance, path, Guid.Empty, out _, out _, out IVsWindowFrame windowFrame);

        windowFrame?.Show();

        var textView = VsShellUtilities.GetTextView(windowFrame);

        if (textView != null)
        {
            textView.SetCaretPos(task.Line, task.Column);
            textView.CenterLines(task.Line, 1);
        }
    }

    private void Delete(string path)
    {
        var tasksForFile = _errorListProvider.Tasks.OfType<ErrorTask>()
             .Where(task => string.Equals(task.Document, path, StringComparison.OrdinalIgnoreCase))
             .ToList();

        foreach (var task in tasksForFile)
        {
            _errorListProvider.Tasks.Remove(task);
        }
    }

    /// <summary>
    /// Handles displaying the results of a file review by presenting the found code smells in the error list.
    /// </summary>
    public void Handle(FileReviewModel review)
    {
        if (review == null || string.IsNullOrWhiteSpace(review.FilePath))
        {
            throw new ArgumentNullException(nameof(review));
        }

        Delete(review.FilePath);

        var issues = review.FunctionLevel.Concat(review.FileLevel).ToList();

        if (!issues.Any()) return;

        Add(issues);
    }
}
