using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;
[Export(typeof(ExtensionEventsManager))]
public class ExtensionEventsManager : IDisposable
{
    [Import]
    private readonly OnDocumentOpenedHandler _onDocumentOpenedHandler;

    [Import]
    private readonly OnDocumentClosedHandler _onDocumentClosedHandler;

    [Import]
    private readonly OnDocumentSavedHandler _onDocumentSavedHandler;

    [Import]
    private readonly OnBeforeDocumentWindowShowHandler _onBeforeDocumentWindowShowHandler;

    [Import]
    private readonly OnAfterDocumentWindowHideHandler _onAfterDocumentWindowHideHandler;

    public void RegisterEvents()
    {
        VS.Events.DocumentEvents.Opened += OnDocumentOpened;
        VS.Events.DocumentEvents.Closed += OnDocumentClosed;
        VS.Events.DocumentEvents.Saved += OnDocumentSaved;
        VS.Events.DocumentEvents.BeforeDocumentWindowShow += OnBeforeDocumentWindowShow;
        VS.Events.DocumentEvents.AfterDocumentWindowHide += OnAfterDocumentWindowHide;

        #region Project & solution events
        //VS.Events.ProjectItemsEvents.AfterRenameProjectItems += OnAfterRenameProjectItems;
        //VS.Events.ProjectItemsEvents.AfterRemoveProjectItems += OnAfterRemoveProjectItems;
        //VS.Events.SolutionEvents.OnAfterOpenProject += OnAfterOpenProject;
        //VS.Events.SolutionEvents.OnBeforeOpenProject += OnBeforeOpenProject;
        //VS.Events.BuildEvents.ProjectConfigurationChanged += OnProjectConfigurationChanged;
        //VS.Events.BuildEvents.SolutionConfigurationChanged += OnSolutionConfigurationChanged;
        #endregion
    }

    public void Dispose()
    {
        VS.Events.DocumentEvents.Opened -= OnDocumentOpened;
        VS.Events.DocumentEvents.Closed -= OnDocumentClosed;
        VS.Events.DocumentEvents.Saved -= OnDocumentSaved;
        VS.Events.DocumentEvents.BeforeDocumentWindowShow -= OnBeforeDocumentWindowShow;
        VS.Events.DocumentEvents.AfterDocumentWindowHide -= OnAfterDocumentWindowHide;

        #region Project & solution events
        //VS.Events.ProjectItemsEvents.AfterRenameProjectItems -= OnAfterRenameProjectItems;
        //VS.Events.ProjectItemsEvents.AfterRemoveProjectItems -= OnAfterRemoveProjectItems;
        //VS.Events.SolutionEvents.OnAfterOpenProject -= OnAfterOpenProject;
        //VS.Events.SolutionEvents.OnBeforeOpenProject -= OnBeforeOpenProject;
        //VS.Events.BuildEvents.ProjectConfigurationChanged -= OnProjectConfigurationChanged;
        //VS.Events.BuildEvents.SolutionConfigurationChanged -= OnSolutionConfigurationChanged;
        #endregion
    }

    private void OnDocumentOpened(string path)
    {
        _onDocumentOpenedHandler.Handle(path);
    }

    private void OnDocumentClosed(string path)
    {
        _onDocumentClosedHandler.Handle(path);
    }

    private void OnDocumentSaved(string path)
    {
        _onDocumentSavedHandler.Handle(path);
    }

    private void OnBeforeDocumentWindowShow(DocumentView doc)
    {
        //Escape show diff window
        if (SkipShowDiffHelper.PathContainsShowDiffFolder(doc.FilePath))
        {
            return;
        }

        _onBeforeDocumentWindowShowHandler.Handle(doc);
    }

    private void OnAfterDocumentWindowHide(DocumentView doc)
    {
        _onAfterDocumentWindowHideHandler.Handle(doc);
    }

    #region Project & solution events
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

    #endregion
}