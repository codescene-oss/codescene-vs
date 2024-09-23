using CodesceneReeinventTest.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace CodesceneReeinventTest.Application;

internal class IssuesHandler : IIssuesHandler
{
    private readonly ErrorListProvider _errorListProvider;
    public IssuesHandler(IServiceProvider serviceProvider)
    {
        // Retrieve it as your package type
        var package = serviceProvider.GetRequiredService<CodesceneReeinventTestPackage>();
        _errorListProvider = new ErrorListProvider(package);
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
}
