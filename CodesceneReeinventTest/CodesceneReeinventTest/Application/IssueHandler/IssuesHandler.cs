using Core.Application.Services.IssueHandler;
using Core.Application.Services.Mapper;
using Core.Models;
using Core.Models.ReviewResult;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.Linq;

namespace CodesceneReeinventTest.Application.IssueHandler;


internal class IssuesHandler : IIssuesHandler
{
    private readonly ErrorListProvider _errorListProvider;
    private readonly IModelMapper _modelMapper;
    private readonly CodesceneReeinventTestPackage _package;
    public IssuesHandler(IServiceProvider serviceProvider, IModelMapper mapper)
    {
        // Retrieve it as your package type
        _package = serviceProvider.GetRequiredService<CodesceneReeinventTestPackage>();
        _errorListProvider = new ErrorListProvider(_package);
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

    private string FormatMessage(ReviewModel i) => $"Codescene - {i.Category} ({i.Details})";
    private void Add(ReviewModel issue)
    {
        var errorTask = new ErrorTask()
        {
            ErrorCategory = TaskErrorCategory.Warning,
            Category = TaskCategory.CodeSense,
            Text = FormatMessage(issue),
            Document = issue.Path,
            Line = issue.StartLine
        };
        errorTask.Navigate += (sender, e) =>
       {
           ThreadHelper.ThrowIfNotOnUIThread();
           VsShellUtilities.OpenDocument(_package, issue.Path, Guid.Empty, out _, out _, out IVsWindowFrame windowFrame);
           windowFrame?.Show();
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

        var issues = _modelMapper.Map(review);
        if (!issues.Any())
        {
            _errorListProvider.Show();//Just show Error list window
            return;
        }

        Add(issues);
    }
}

public class Test : IVsHierarchy
{
    public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
    {
        throw new NotImplementedException();
    }

    public int GetSite(out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP)
    {
        throw new NotImplementedException();
    }

    public int QueryClose(out int pfCanClose)
    {
        throw new NotImplementedException();
    }

    public int Close()
    {
        throw new NotImplementedException();
    }

    public int GetGuidProperty(uint itemid, int propid, out Guid pguid)
    {
        throw new NotImplementedException();
    }

    public int SetGuidProperty(uint itemid, int propid, ref Guid rguid)
    {
        throw new NotImplementedException();
    }

    public int GetProperty(uint itemid, int propid, out object pvar)
    {
        throw new NotImplementedException();
    }

    public int SetProperty(uint itemid, int propid, object var)
    {
        throw new NotImplementedException();
    }

    public int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pitemidNested)
    {
        throw new NotImplementedException();
    }

    public int GetCanonicalName(uint itemid, out string pbstrName)
    {
        throw new NotImplementedException();
    }

    public int ParseCanonicalName(string pszName, out uint pitemid)
    {
        throw new NotImplementedException();
    }

    public int Unused0()
    {
        throw new NotImplementedException();
    }

    public int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie)
    {
        throw new NotImplementedException();
    }

    public int UnadviseHierarchyEvents(uint dwCookie)
    {
        throw new NotImplementedException();
    }

    public int Unused1()
    {
        throw new NotImplementedException();
    }

    public int Unused2()
    {
        throw new NotImplementedException();
    }

    public int Unused3()
    {
        throw new NotImplementedException();
    }

    public int Unused4()
    {
        throw new NotImplementedException();
    }
}