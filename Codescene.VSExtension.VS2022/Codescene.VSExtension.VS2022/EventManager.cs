using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Linq;

namespace Codescene.VSExtension.VS2022;
public class EventManager : IDisposable
{
    public void RegisterEvents()
    {
        VS.Events.DocumentEvents.AfterDocumentWindowHide += OnDocumentWindowHide;
        VS.Events.DocumentEvents.Opened += OnDocumentOpened;
        VS.Events.DocumentEvents.Closed += OnDocumentClosed;
        VS.Events.DocumentEvents.BeforeDocumentWindowShow += OnBeforeDocumentWindowShow;
        VS.Events.ProjectItemsEvents.AfterRenameProjectItems += OnAfterRenameProjectItems;
        VS.Events.ProjectItemsEvents.AfterRemoveProjectItems += OnAfterRemoveProjectItems;
        VS.Events.SolutionEvents.OnAfterOpenProject += OnAfterOpenProject;
        VS.Events.SolutionEvents.OnBeforeOpenProject += OnBeforeOpenProject;
        VS.Events.BuildEvents.ProjectConfigurationChanged += OnProjectConfigurationChanged;
        VS.Events.BuildEvents.SolutionConfigurationChanged += OnSolutionConfigurationChanged;
    }

    private void OnDocumentWindowHide(DocumentView obj)
    {
        VS.StatusBar.ShowMessageAsync(obj.Document?.FilePath ?? "").FireAndForget();
    }

    private void OnDocumentOpened(string obj)
    {
        VS.StatusBar.ShowMessageAsync("Opened document " + (obj ?? "no name")).FireAndForget();
    }

    private void OnDocumentClosed(string obj)
    {
        VS.StatusBar.ShowMessageAsync("Closed document " + (obj ?? "no name")).FireAndForget();
    }

    private void OnBeforeDocumentWindowShow(DocumentView obj)
    {
        VS.StatusBar.ShowMessageAsync(obj.Document?.FilePath ?? "").FireAndForget();
    }

    private void OnAfterRenameProjectItems(AfterRenameProjectItemEventArgs obj)
    {
        string info = string.Join(",", obj.ProjectItemRenames.Select(x => $"{x.SolutionItem.Name}:{x.OldName}"));
        VS.MessageBox.ShowConfirm(info);
    }

    private void OnAfterRemoveProjectItems(AfterRemoveProjectItemEventArgs obj)
    {
        string info = string.Join(",", obj.ProjectItemRemoves.Select(x => $"{x.Project.Name}:{x.RemovedItemName}"));
        VS.MessageBox.ShowConfirm(info);
    }

    private void OnBeforeOpenProject(string obj)
    {
        VS.StatusBar.ShowMessageAsync("About to open " + obj).FireAndForget();
    }

    private void OnAfterOpenProject(Project obj)
    {
        if (obj != null)
        {
            VS.StatusBar.ShowMessageAsync("Opened project " + obj.Name).FireAndForget();
        }
    }

    private void OnProjectConfigurationChanged(Project? obj)
    {
        if (obj != null)
        {
            VS.StatusBar.ShowMessageAsync($"Configuration for project {obj.Name} changed").FireAndForget();
        }
    }

    private void OnSolutionConfigurationChanged()
    {
        VS.StatusBar.ShowMessageAsync("Solution configuration changed").FireAndForget();
    }

    public void Dispose()
    {
        VS.Events.DocumentEvents.AfterDocumentWindowHide -= OnDocumentWindowHide;
        VS.Events.DocumentEvents.Opened -= OnDocumentOpened;
        VS.Events.DocumentEvents.Closed -= OnDocumentClosed;
        VS.Events.DocumentEvents.BeforeDocumentWindowShow -= OnBeforeDocumentWindowShow;
        VS.Events.ProjectItemsEvents.AfterRenameProjectItems -= OnAfterRenameProjectItems;
        VS.Events.ProjectItemsEvents.AfterRemoveProjectItems -= OnAfterRemoveProjectItems;
        VS.Events.SolutionEvents.OnAfterOpenProject -= OnAfterOpenProject;
        VS.Events.SolutionEvents.OnBeforeOpenProject -= OnBeforeOpenProject;
        VS.Events.BuildEvents.ProjectConfigurationChanged -= OnProjectConfigurationChanged;
        VS.Events.BuildEvents.SolutionConfigurationChanged -= OnSolutionConfigurationChanged;
    }
}

