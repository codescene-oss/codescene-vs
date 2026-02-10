// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.VS2022.Application.Git;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Listeners;

/// <summary>
/// Handles solution-level events in Visual Studio (e.g. when a solution is opened or closed).
/// Used to trigger cleanup or updates tied to the lifecycle of a loaded solution.
/// </summary>
public class SolutionEventsHandler : IVsSolutionEvents, IDisposable
{
    private uint _cookie;
    private IVsSolution _solution;
    private BranchWatcherService _branchWatcher;
    private IGitChangeObserver _gitChangeObserver;

    /// <summary>
    /// Subscribes to solution events using the Visual Studio shell service.
    /// </summary>
    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
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
        }
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

            CodeSceneToolWindow.UpdateViewAsync().FireAndForget();
            AceToolWindow.CloseAsync().FireAndForget();
            CodeSmellDocumentationWindow.HideAsync().FireAndForget();

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
        _ = Task.Run(async () =>
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();
            var solutionPath = solution?.FullPath;
            if (string.IsNullOrEmpty(solutionPath))
            {
                return;
            }

            _branchWatcher = new BranchWatcherService();
            _branchWatcher.StartWatching(solutionPath, (newBranch) => OnBranchChangedAsync(newBranch).FireAndForget());

            await InitializeGitChangeObserverAsync(solutionPath);
        });

        return VSConstants.S_OK;
    }

    public int OnBeforeCloseSolution(object pUnkReserved)
    {
        _branchWatcher?.Dispose();
        _branchWatcher = null;
        _gitChangeObserver?.Dispose();
        _gitChangeObserver = null;
        return VSConstants.S_OK;
    }

    // The remaining event methods are currently unused, but required by the IVsSolutionEvents interface.
    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.S_OK;

    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;

    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;

    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var canDispose = ThreadHelper.CheckAccess() && _solution != null && _cookie != 0;

        if (canDispose)
        {
            _solution.UnadviseSolutionEvents(_cookie);
            _cookie = 0;
        }
    }

    private static void Log(Func<ILogger, Task> logAction)
    {
        _ = Task.Run(async () =>
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();
            if (logger != null)
            {
                await logAction(logger);
            }
        });
    }

    private async Task InitializeGitChangeObserverAsync(string solutionPath)
    {
        try
        {
            _gitChangeObserver = await VS.GetMefServiceAsync<IGitChangeObserver>();
            if (_gitChangeObserver == null)
            {
                Log(logger =>
                {
                    logger.Warn("Failed to obtain IGitChangeObserver service.");
                    return Task.CompletedTask;
                });
                return;
            }

            var savedFilesTracker = await VS.GetMefServiceAsync<ISavedFilesTracker>();
            var openFilesObserver = await VS.GetMefServiceAsync<IOpenFilesObserver>();

            if (savedFilesTracker == null || openFilesObserver == null)
            {
                Log(logger =>
                {
                    logger.Warn("Failed to obtain required services for GitChangeObserver.");
                    return Task.CompletedTask;
                });
                return;
            }

            _gitChangeObserver.Initialize(solutionPath, savedFilesTracker, openFilesObserver);
            _gitChangeObserver.Start();

            Log(logger =>
            {
                logger.Info("GitChangeObserver initialized and started.");
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            Log(logger =>
            {
                logger.Error("Failed to initialize GitChangeObserver.", ex);
                return Task.CompletedTask;
            });
        }
    }

    private async Task OnBranchChangedAsync(string newBranch)
    {
        try
        {
            Log(logger =>
            {
                logger.Info($"Branch switched to: '{newBranch}'. Clearing delta cache...");
                return Task.CompletedTask;
            });

            var cache = new DeltaCacheService();
            cache.Clear();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            CodeSceneToolWindow.UpdateViewAsync().FireAndForget();
        }
        catch (Exception ex)
        {
            Log(logger =>
            {
                logger.Error($"Failed handling branch switch event: {newBranch}", ex);
                return Task.CompletedTask;
            });
        }
    }
}
