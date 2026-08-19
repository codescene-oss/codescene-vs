// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.VS2022.Application.Git;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Codescene.VSExtension.VS2022.Handlers;

/// <summary>
/// Handles solution-level events in Visual Studio (e.g. when a solution is opened or closed).
/// Used to trigger cleanup or updates tied to the lifecycle of a loaded solution.
/// </summary>
public class SolutionEventsHandler : IVsSolutionEvents, IDisposable
{
    private readonly object _initGate = new object();
    private uint _cookie;
    private IVsSolution _solution;
    private IWorkspaceReviewCoordinator _workspaceReviewCoordinator;
    private IAsyncTaskScheduler _scheduler;
    private IErrorListWindowHandler _errorListWindowHandler;
    private Task _solutionInitializationTask;

    /// <summary>
    /// Subscribes to solution events using the Visual Studio shell service.
    /// </summary>
    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        _scheduler = await VS.GetMefServiceAsync<IAsyncTaskScheduler>();
        _errorListWindowHandler = await VS.GetMefServiceAsync<IErrorListWindowHandler>();

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var isUiThread = ThreadHelper.CheckAccess();

        if (isUiThread)
        {
            var solution = (IVsSolution)serviceProvider.GetService(typeof(SVsSolution));
            if (solution != null)
            {
                solution.AdviseSolutionEvents(this, out _cookie);
                _solution = solution;
            }

            VS.Events.SolutionEvents.OnAfterCloseFolder += SolutionEvents_OnAfterCloseFolder;
            VS.Events.SolutionEvents.OnBeforeCloseFolder += SolutionEvents_OnBeforeCloseFolder;
            VS.Events.SolutionEvents.OnAfterOpenFolder += SolutionEvents_OnAfterOpenFolder;
        }

        _scheduler.Schedule(ct => EnsureSolutionInitializationAsync());
    }

    /// <summary>
    /// Called after a solution or folder is closed. Clears delta analysis cache and updates UI.
    /// </summary>
    public int OnAfterCloseSolution(object pUnkReserved)
    {
        try
        {
            var cache = new DeltaCacheService();
            cache.Clear();
            new BaselineReviewCacheService().Clear();
            new ReviewCacheService().Clear();
            new AceRefactorableFunctionsCacheService().Clear();
            CacheGeneration.Increment();

            _scheduler.Schedule(ct => CodeSceneToolWindow.UpdateViewAsync());
            _scheduler.Schedule(ct => AceToolWindow.CloseAsync());
            _scheduler.Schedule(ct => CodeSmellDocumentationWindow.HideAsync());

            _scheduler.Schedule(async ct =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _errorListWindowHandler?.ClearAll();
            });

            _scheduler.Schedule(async ct =>
            {
                var savedFilesTracker = await VS.GetMefServiceAsync<ISavedFilesTracker>();
                savedFilesTracker?.ClearSavedFiles();
            });

            Log(logger =>
            {
                logger.Info("Solution or folder was closed. Clearing delta cache...");
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            Log(logger =>
            {
                logger.Error("Unable to clear delta cache on close of solution.", ex);
                return Task.CompletedTask;
            });
        }

        return VSConstants.S_OK;
    }

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
        _scheduler.Schedule(ct => EnsureSolutionInitializationAsync());
        return VSConstants.S_OK;
    }

    public int OnBeforeCloseSolution(object pUnkReserved)
    {
        lock (_initGate)
        {
            _solutionInitializationTask = null;
        }

        _workspaceReviewCoordinator?.Stop();
        return VSConstants.S_OK;
    }

    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
    {
        _scheduler.Schedule(ct => RefreshWorkspacePathsAsync());
        return VSConstants.S_OK;
    }

    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
    {
        _scheduler.Schedule(ct => RefreshWorkspacePathsAsync());
        return VSConstants.S_OK;
    }

    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;

    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var canDispose = ThreadHelper.CheckAccess();

        if (canDispose)
        {
            if (_solution != null && _cookie != 0)
            {
                _solution.UnadviseSolutionEvents(_cookie);
                _cookie = 0;
            }

            VS.Events.SolutionEvents.OnAfterCloseFolder -= SolutionEvents_OnAfterCloseFolder;
            VS.Events.SolutionEvents.OnBeforeCloseFolder -= SolutionEvents_OnBeforeCloseFolder;
            VS.Events.SolutionEvents.OnAfterOpenFolder -= SolutionEvents_OnAfterOpenFolder;
        }

        _workspaceReviewCoordinator?.Stop();
    }

    private void Log(Func<ILogger, Task> logAction)
    {
        _scheduler.Schedule(async ct =>
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();
            await logAction(logger);
        });
    }

    private void SolutionEvents_OnAfterOpenFolder(string obj)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        OnAfterOpenSolution(null, 0);
    }

    private void SolutionEvents_OnAfterCloseFolder(string obj)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        OnAfterCloseSolution(null);
    }

    private void SolutionEvents_OnBeforeCloseFolder(string obj)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        OnBeforeCloseSolution(null);
    }

    private async Task EnsureSolutionInitializationAsync()
    {
        var solution = await VS.Solutions.GetCurrentSolutionAsync();
        var solutionPath = solution?.FullPath;
        if (string.IsNullOrEmpty(solutionPath))
        {
            return;
        }

        Task task;
        lock (_initGate)
        {
            if (_solutionInitializationTask != null)
            {
                task = _solutionInitializationTask;
            }
            else
            {
                _solutionInitializationTask = RunSolutionInitializationAsync(solutionPath);
                task = _solutionInitializationTask;
            }
        }

        await task;
    }

    private async Task RunSolutionInitializationAsync(string solutionPath)
    {
        new DeltaCacheService().Clear();
        new BaselineReviewCacheService().Clear();
        new ReviewCacheService().Clear();
        new AceRefactorableFunctionsCacheService().Clear();

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        _errorListWindowHandler?.ClearAll();

        await InitializeWorkspaceReviewAsync(solutionPath);
    }

    private async Task InitializeWorkspaceReviewAsync(string solutionPath)
    {
        try
        {
            _workspaceReviewCoordinator = await VS.GetMefServiceAsync<IWorkspaceReviewCoordinator>();
            if (_workspaceReviewCoordinator == null)
            {
                Log(logger =>
                {
                    logger.Warn("Failed to obtain IWorkspaceReviewCoordinator service.");
                    return Task.CompletedTask;
                });
                return;
            }

            IReadOnlyCollection<string> workspacePaths = null;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_solution != null)
            {
                workspacePaths = SolutionProjectDiscovery.GetProjectDirectories(_solution, solutionPath);
            }

            await _workspaceReviewCoordinator.StartAsync(solutionPath, workspacePaths);

            Log(logger =>
            {
                logger.Info("Workspace review coordinator started.");
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            Log(logger =>
            {
                logger.Error("Failed to start workspace review coordinator.", ex);
                return Task.CompletedTask;
            });
        }
    }

    private async Task RefreshWorkspacePathsAsync()
    {
        if (_workspaceReviewCoordinator == null)
        {
            return;
        }

        var solution = await VS.Solutions.GetCurrentSolutionAsync();
        var solutionPath = solution?.FullPath;
        if (string.IsNullOrEmpty(solutionPath))
        {
            return;
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (_solution == null)
        {
            return;
        }

        var workspacePaths = SolutionProjectDiscovery.GetProjectDirectories(_solution, solutionPath);
        await _workspaceReviewCoordinator.StartAsync(solutionPath, workspacePaths);
    }
}
