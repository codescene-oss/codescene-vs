using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.VS2022.Application.ErrorListWindowHandler;

[Export(typeof(IErrorListWindowHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class ErrorListWindowHandler : IErrorListWindowHandler
{
    private readonly ErrorListProvider _errorListProvider;

    [Import]
    private readonly IModelMapper _modelMapper;

    public ErrorListWindowHandler()
    {
        _errorListProvider = new ErrorListProvider(VS2022Package.Instance);
    }

    //public string GetUrl()
    //{
    //    return "www.google.com";
    //}

    //public void Handle(IEnumerable<IssueModel> issues)
    //{
    //    foreach (var issue in issues)
    //    {
    //        Add(issue);
    //    }
    //}

    private void Add(IEnumerable<CodeSmellModel> issues)
    {
        foreach (var issue in issues)
        {
            Add(issue);
        }
    }

    private string FormatMessage(CodeSmellModel i) => $"Codescene - {i.Category} ({i.Details})";

    private void Add(CodeSmellModel issue)
    {
        var errorTask = new ErrorTask()
        {
            ErrorCategory = TaskErrorCategory.Warning,
            Category = TaskCategory.CodeSense,
            Text = FormatMessage(issue),
            Document = issue.Path,
            Line = issue.StartLine
        };
        errorTask.Navigate += OpenDocumentWithIssue;
        _errorListProvider.Tasks.Add(errorTask);
        _errorListProvider.Show();
    }

    private void OpenDocumentWithIssue(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.OpenDocument(VS2022Package.Instance, _filePath, Guid.Empty, out _, out _, out IVsWindowFrame windowFrame);
        windowFrame?.Show();
    }

    //private async void AddWarnings(string filePath)
    //{
    //    var errorListProvider = new ErrorListProvider(ServiceProvider.GlobalProvider);
    //    var review = _cliExecuter.GetReviewObject(filePath);

    //    foreach (var issues in review.Review)
    //    {
    //        foreach (var function in issues.Functions)
    //        {
    //            var errorTask = new ErrorTask
    //            {

    //                Text = issues.Category + " (" + function.Details + ")",
    //                Document = filePath,
    //                Line = function.Startline - 1,
    //                Column = function.Startline,
    //                Category = TaskCategory.BuildCompile,
    //                ErrorCategory = TaskErrorCategory.Warning,
    //            };
    //            errorListProvider.Tasks.Add(errorTask);
    //        }
    //    }
    //    errorListProvider.Show();
    //}

    //private void Add(IssueModel issue)
    //{
    //    // Get the ErrorListProvider

    //    // Create a new ErrorTask
    //    var errorTask = new ErrorTask()
    //    {
    //        ErrorCategory = TaskErrorCategory.Warning, // Can be Error, Warning, or Message
    //        Category = TaskCategory.BuildCompile,
    //        Text = issue.Message,
    //        Document = issue.Resource,
    //        Line = issue.StartLineNumber,
    //        Column = issue.EndLineNumber,
    //    };
    //    // Add a handler for when the user clicks on the error
    //    /*errorTask.Navigate += (sender, e) =>
    //    {
    //        // This can be customized to open the file and go to the error location
    //        // Example:
    //        IVsWindowFrame windowFrame;
    //        VsShellUtilities.OpenDocument(serviceProvider, document, Guid.Empty, out windowFrame, out _, out _, out _);
    //        windowFrame?.Show();
    //    };*/
    //    // Add the error task to the Error List
    //    _errorListProvider.Tasks.Add(errorTask);
    //    // Make sure the Error List is visible
    //    _errorListProvider.Show();
    //}

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

    //public void Handle(string filePath, CsReview review)
    //{
    //    Delete(filePath);

    //    var issues = _modelMapper.Map(review);
    //    if (!issues.Any())
    //    {
    //        _errorListProvider.Show();//Just show Error list window
    //        return;
    //    }

    //    Add(issues);
    //}

    private string _filePath = string.Empty;
    public void Handle(string filePath, FileReviewModel review)
    {
        _filePath = filePath;
        Delete(filePath);

        var issues = review.FunctionLevel;
        if (!issues.Any())
        {
            _errorListProvider.Show();//Just show Error list window
            return;
        }

        Add(issues);
    }
}
