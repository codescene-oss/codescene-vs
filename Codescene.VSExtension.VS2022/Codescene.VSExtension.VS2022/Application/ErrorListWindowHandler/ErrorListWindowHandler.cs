using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
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
        Task.Run(async () =>
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            try
            {
                logger.Debug($"Opening document '{path}' from error list...");

                var task = sender as ErrorTask;
                var isUiThread = ThreadHelper.CheckAccess();

                if (!isUiThread)
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (task?.Line == null)
                {
                    logger.Warn($"Could not open document '{path}' from error list, focus line is not valid.");
                    return;
                }

                await DocumentNavigator.OpenFileAndGoToLineAsync(path, task.Line + 1, logger);
                logger.Debug($"Opened document '{path}' from error list...");
            }
            catch (Exception e)
            {
                logger.Error($"Unable to open document '{path}'", e);
            }
        }).FireAndForget();
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
            return;

        Delete(review.FilePath);

        var issues = review.FunctionLevel.Concat(review.FileLevel).ToList();

        if (!issues.Any()) return;

        Add(issues);
    }
}
