// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorListWindowHandler;

[Export(typeof(IErrorListWindowHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class ErrorListWindowHandler : IErrorListWindowHandler
{
    private ErrorListProvider _errorListProvider;

    private ErrorListProvider ErrorListProvider
    {
        get
        {
            if (_errorListProvider == null && VS2022Package.Instance != null)
            {
                _errorListProvider = new ErrorListProvider(VS2022Package.Instance);
            }

            return _errorListProvider;
        }
    }

    /// <summary>
    /// Handles displaying the results of a file review by presenting the found code smells in the error list.
    /// </summary>
    public void Handle(FileReviewModel review)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (review == null || string.IsNullOrWhiteSpace(review.FilePath))
        {
            return;
        }

        Delete(review.FilePath);

        var issues = review.FunctionLevel.Concat(review.FileLevel).ToList();

        if (!issues.Any())
        {
            return;
        }

        Add(issues);
    }

    public void ClearAll()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ErrorListProvider?.Tasks?.Clear();
    }

    private void Add(IEnumerable<CodeSmellModel> issues)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        foreach (var issue in issues)
        {
            Add(issue);
        }
    }

    private string FormatMessage(CodeSmellModel codeSmell, bool includeDetails = true)
    {
        var title = $"{(!string.IsNullOrEmpty(codeSmell.FunctionName) ? codeSmell.FunctionName + " - " : string.Empty)}{codeSmell.Category}";

        if (includeDetails)
        {
            if (!string.IsNullOrEmpty(codeSmell.Details))
            {
                title += $" ({codeSmell.Details})";
            }

            if (codeSmell.FunctionRange?.StartLine != null && codeSmell.FunctionRange?.StartColumn != null)
            {
                title += $" [Ln {codeSmell.FunctionRange.StartLine}, Col {codeSmell.FunctionRange.StartColumn}]";
            }
        }

        return title;
    }

    private void Add(CodeSmellModel issue)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var errorTask = new ErrorTask
        {
            ErrorCategory = TaskErrorCategory.Warning,
            Category = TaskCategory.CodeSense,
            Text = FormatMessage(issue),
            Document = issue.Path,
            Line = issue.Range.StartLine - 1, // 0-based field
            Column = issue.Range.StartColumn - 1, // 0-based field
            HierarchyItem = HierarchyHelper.GetHierarchyFromFile(issue.Path),
            SubcategoryIndex = 2,
            HelpKeyword = FormatMessage(issue, false),
        };
        errorTask.Navigate += (sender, _) => { OpenDocumentWithIssue(sender, issue.Path); };
        ErrorListProvider?.Tasks?.Add(errorTask);
    }

    private void OpenDocumentWithIssue(object sender, string path)
    {
        var package = VS2022Package.Instance;
        if (package == null)
        {
            return;
        }

        package.JoinableTaskFactory.RunAsync(async () =>
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            try
            {
                logger.Debug($"Opening document '{path}' from error list...");

                var task = sender as ErrorTask;
                var isUiThread = ThreadHelper.CheckAccess();

                if (!isUiThread)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                }

                if (task?.Line == null)
                {
                    logger.Warn($"Could not open document '{path}' from error list, focus line is not valid.");
                    return;
                }

                await DocumentNavigator.OpenFileAndGoToLineAsync(path, task.Line + 1, logger);
                logger.Debug($"Opened document '{path}' from error list...");
            }
            catch (Exception ex)
            {
                logger.Error($"Unable to open document '{path}'", ex);
            }
        }).FileAndForget("ErrorListWindowHandler/Handle");
    }

    private void Delete(string path)
    {
        var tasksForFile = ErrorListProvider?.Tasks?.OfType<ErrorTask>()
             .Where(task => string.Equals(task.Document, path, StringComparison.OrdinalIgnoreCase))
             .ToList();

        if (tasksForFile == null)
        {
            return;
        }

        foreach (var task in tasksForFile)
        {
            ErrorListProvider?.Tasks?.Remove(task);
        }
    }
}
