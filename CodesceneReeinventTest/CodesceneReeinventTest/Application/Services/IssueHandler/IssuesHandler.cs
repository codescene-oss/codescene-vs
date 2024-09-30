using CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;
using CodesceneReeinventTest.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

using System.Linq;

namespace CodesceneReeinventTest.Application;

internal class IssuesHandler : IIssuesHandler
{
    private readonly ErrorListProvider _errorListProvider;
    private readonly IModelMapper _modelMapper;
    public IssuesHandler(IServiceProvider serviceProvider, IModelMapper mapper)
    {
        // Retrieve it as your package type
        var package = serviceProvider.GetRequiredService<CodesceneReeinventTestPackage>();
        _errorListProvider = new ErrorListProvider(package);
        _modelMapper = mapper;
    }
    public string GetUrl()
    {
        return "www.google.com";
    }

    public void Handle(IEnumerable<IssueModel> issues)
    {
        foreach (var issue in issues)
        {
            Add(issue);
        }
    }

    private void Add(IEnumerable<ReviewModel> issues)
    {
        foreach (var issue in issues)
        {
            Add(issue);
        }
    }

    private string FormatMessage(ReviewModel i) => $"{i.Category} ({i.Details}) Codescene({i.Category})";
    private void Add(ReviewModel issue)
    {
        var errorTask = new ErrorTask()
        {
            ErrorCategory = TaskErrorCategory.Warning,
            Category = TaskCategory.BuildCompile,
            Text = FormatMessage(issue),
            Document = issue.Path,
            Line = issue.StartLine,
            Column = issue.EndLine
        };
        _errorListProvider.Tasks.Add(errorTask);
        _errorListProvider.Show();
    }

    private void Add(IssueModel issue)
    {
        // Get the ErrorListProvider

        // Create a new ErrorTask
        var errorTask = new ErrorTask()
        {
            ErrorCategory = TaskErrorCategory.Warning, // Can be Error, Warning, or Message
            Category = TaskCategory.BuildCompile,
            Text = issue.Message,
            Document = issue.Resource,
            Line = issue.StartLineNumber,
            Column = issue.EndLineNumber,
        };
        // Add a handler for when the user clicks on the error
        /*errorTask.Navigate += (sender, e) =>
        {
            // This can be customized to open the file and go to the error location
            // Example:
            IVsWindowFrame windowFrame;
            VsShellUtilities.OpenDocument(serviceProvider, document, Guid.Empty, out windowFrame, out _, out _, out _);
            windowFrame?.Show();
        };*/
        // Add the error task to the Error List
        _errorListProvider.Tasks.Add(errorTask);
        // Make sure the Error List is visible
        _errorListProvider.Show();
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

    public void Handle(string filePath, CsReview review)
    {
        Delete(filePath);
        if (string.IsNullOrWhiteSpace(review.RawScore?.Name))
        {
            review.RawScore = new Application.Services.FileReviewer.ReviewResult.RawScore { Name = filePath };
        }
        var issues = _modelMapper.Map(review);
        if (!issues.Any())
        {
            _errorListProvider.Show();//Just show Error list window
            return;
        }

        Add(issues);
    }
}
