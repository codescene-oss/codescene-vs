// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public class SolutionEventsHandler : IVsSolutionEvents, IVsSolutionEvents7, IDisposable
{
    private readonly object _initGate = new object();
    private uint _cookie;
    private IVsSolution _solution;
    private BranchWatcherService _branchWatcher;
    private IWorkspaceWatchCoordinator _workspaceWatch;
    private IReviewPipeline _reviewPipeline;
    private ICodeHealthMonitorNotifier _codeHealthMonitorNotifier;
    private IAsyncTaskScheduler _scheduler;
    private IErrorListWindowHandler _errorListWindowHandler;
    private Task _solutionInitializationTask;
    private string _initializedWorkspacePath;
    private int _initGeneration;

    /// <summary>
    /// Subscribes to solution events using the Visual Studio shell service.
    /// </summary>
    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        _scheduler = await VS.GetMefServiceAsync<IAsyncTaskScheduler>();
        _errorListWindowHandler = await VS.GetMefServiceAsync<IErrorListWindowHandler>();
        _reviewPipeline = await VS.GetMefServiceAsync<IReviewPipeline>();
        _codeHealthMonitorNotifier = await VS.GetMefServiceAsync<ICodeHealthMonitorNotifier>();

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

        ScheduleWorkspaceInitialization(null);
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
            new ReviewCacheService().Clear();
            new AceRefactorableFunctionsCacheService().Clear();
            CacheGeneration.Increment();
            _codeHealthMonitorNotifier?.Clear();

            _scheduler.Schedule(ct => CodeSceneToolWindow.UpdateViewAsync());
            _scheduler.Schedule(ct => AceToolWindow.CloseAsync());
            _scheduler.Schedule(ct => CodeSmellDocumentationWindow.HideAsync());

            _scheduler.Schedule(async ct =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _errorListWindowHandler?.ClearAll();
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
        ScheduleWorkspaceInitialization(null);
        return VSConstants.S_OK;
    }

    public int OnBeforeCloseSolution(object pUnkReserved)
    {
        lock (_initGate)
        {
            _solutionInitializationTask = null;
            _initializedWorkspacePath = null;
            _initGeneration++;
            _reviewPipeline?.SuspendBufferReviews();
        }

        _branchWatcher?.Dispose();
        _branchWatcher = null;
        _workspaceWatch?.StopAll();
        _reviewPipeline?.Reset();
        _reviewPipeline?.SetActiveRepos(Array.Empty<string>());
        _codeHealthMonitorNotifier?.Clear();
        return VSConstants.S_OK;
    }

    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
    {
        return ScheduleWorkspaceRefresh();
    }

    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
    {
        _scheduler.Schedule(ct => RefreshWorkspacePathsAsync());
        return VSConstants.S_OK;
    }

    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
    {
        return ScheduleWorkspaceRefresh();
    }

    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;

    void IVsSolutionEvents7.OnAfterOpenFolder(string folderPath)
    {
        SolutionEvents_OnAfterOpenFolder(folderPath);
    }

    void IVsSolutionEvents7.OnBeforeCloseFolder(string folderPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        SolutionEvents_OnBeforeCloseFolder(folderPath);
    }

    void IVsSolutionEvents7.OnQueryCloseFolder(string folderPath, ref int pfCancel)
    {
    }

    void IVsSolutionEvents7.OnAfterCloseFolder(string folderPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        SolutionEvents_OnAfterCloseFolder(folderPath);
    }

    void IVsSolutionEvents7.OnAfterLoadAllDeferredProjects()
    {
        ScheduleWorkspaceRefresh();
    }

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
    }

    private void Log(Func<ILogger, Task> logAction)
    {
        _scheduler.Schedule(async ct =>
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();
            await logAction(logger);
        });
    }

    private int ScheduleWorkspaceRefresh()
    {
        if (_workspaceWatch == null)
        {
            ScheduleWorkspaceInitialization(null);
            return VSConstants.S_OK;
        }

        _scheduler.Schedule(ct => RefreshWorkspacePathsAsync());
        return VSConstants.S_OK;
    }

    private void ScheduleWorkspaceInitialization(string explicitPath)
    {
        _scheduler.Schedule(ct => InitializeWorkspaceSafeAsync(explicitPath));
    }

    private async Task InitializeWorkspaceSafeAsync(string explicitPath)
    {
        try
        {
            await EnsureSolutionInitializationAsync(explicitPath);
        }
        catch (Exception ex)
        {
            Log(logger =>
            {
                logger.Error("Failed to initialize workspace.", ex);
                return Task.CompletedTask;
            });
        }
    }

    private void SolutionEvents_OnAfterOpenFolder(string folderPath)
    {
        Log(logger =>
        {
            logger.Info($"Folder opened: '{folderPath}'.");
            return Task.CompletedTask;
        });
        ScheduleWorkspaceInitialization(folderPath);
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

    private async Task EnsureSolutionInitializationAsync(string explicitPath)
    {
        var solutionPath = explicitPath;
        if (string.IsNullOrEmpty(solutionPath))
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();
            solutionPath = solution?.FullPath;
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            solutionPath = GetOpenFolderPath();
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            Log(logger =>
            {
                logger.Debug("No workspace open; workspace watch not started.");
                return Task.CompletedTask;
            });
            return;
        }

        var folderWorkspace = Directory.Exists(solutionPath);
        Task task;
        lock (_initGate)
        {
            if (_solutionInitializationTask != null
                && string.Equals(_initializedWorkspacePath, solutionPath, StringComparison.OrdinalIgnoreCase))
            {
                task = _solutionInitializationTask;
            }
            else
            {
                var generation = ++_initGeneration;
                _initializedWorkspacePath = solutionPath;
                _solutionInitializationTask = RunSolutionInitializationAsync(solutionPath, folderWorkspace, generation);
                task = _solutionInitializationTask;
            }
        }

        await task;
    }

    private async Task RunSolutionInitializationAsync(string solutionPath, bool folderWorkspace, int generation)
    {
        if (!IsCurrentInitialization(generation))
        {
            return;
        }

        ResumeBufferReviewsIfCurrent(generation);
        ResetWorkspaceForInitialization();
        ClearAnalysisCaches();
        _scheduler.Schedule(ct => CodeSceneToolWindow.UpdateViewAsync());

        if (!IsCurrentInitialization(generation))
        {
            return;
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        _errorListWindowHandler?.ClearAll();

        _branchWatcher = new BranchWatcherService();
        _branchWatcher.StartWatching(solutionPath, (newBranch) =>
            _scheduler.Schedule(ct2 => OnBranchChangedAsync(newBranch)));

        await InitializeWorkspaceWatchAsync(solutionPath, folderWorkspace, generation);
    }

    private async Task InitializeWorkspaceWatchAsync(string solutionPath, bool folderWorkspace, int generation)
    {
        try
        {
            var coordinator = await PrepareWorkspaceWatchAsync(generation);
            if (coordinator == null)
            {
                return;
            }

            var workspacePaths = await ResolveWorkspacePathsAsync(solutionPath, folderWorkspace, generation);
            if (workspacePaths == null)
            {
                return;
            }

            await coordinator.SyncAsync(new List<string>(workspacePaths));

            Log(logger =>
            {
                logger.Info("Workspace watch initialized.");
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            Log(logger =>
            {
                logger.Error("Failed to initialize workspace watch.", ex);
                return Task.CompletedTask;
            });
        }
    }

    private async Task<IWorkspaceWatchCoordinator> PrepareWorkspaceWatchAsync(int generation)
    {
        if (!IsCurrentInitialization(generation))
        {
            return null;
        }

        _workspaceWatch = await VS.GetMefServiceAsync<IWorkspaceWatchCoordinator>();
        if (_workspaceWatch == null)
        {
            Log(logger =>
            {
                logger.Warn("Failed to obtain IWorkspaceWatchCoordinator service.");
                return Task.CompletedTask;
            });
            return null;
        }

        if (!IsCurrentInitialization(generation))
        {
            return null;
        }

        return _workspaceWatch;
    }

    private async Task<IReadOnlyCollection<string>> ResolveWorkspacePathsAsync(string solutionPath, bool folderWorkspace, int generation)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        IReadOnlyCollection<string> workspacePaths = Array.Empty<string>();
        if (folderWorkspace)
        {
            workspacePaths = SolutionProjectDiscovery.GetProjectDirectories(null, solutionPath);
        }
        else if (_solution != null)
        {
            workspacePaths = SolutionProjectDiscovery.GetProjectDirectories(_solution, solutionPath);
        }

        if (!IsCurrentInitialization(generation))
        {
            return null;
        }

        return workspacePaths;
    }

    private async Task RefreshWorkspacePathsAsync()
    {
        if (_workspaceWatch == null)
        {
            return;
        }

        string solutionPath;
        int generation;
        lock (_initGate)
        {
            solutionPath = _initializedWorkspacePath;
            generation = _initGeneration;
        }

        if (string.IsNullOrEmpty(solutionPath))
        {
            return;
        }

        var watchSession = _workspaceWatch.WatchSession;
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var workspacePaths = CurrentProjectDirectories(solutionPath, generation, watchSession);
        if (workspacePaths == null)
        {
            return;
        }

        await _workspaceWatch.SyncAsync(new List<string>(workspacePaths), watchSession);
    }

    private IReadOnlyCollection<string> CurrentProjectDirectories(string solutionPath, int generation, long watchSession)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!RefreshStillCurrent(generation, watchSession))
        {
            return null;
        }

        var workspacePaths = ProjectDirectories(solutionPath);
        if (workspacePaths == null || !RefreshStillCurrent(generation, watchSession))
        {
            return null;
        }

        return workspacePaths;
    }

    private bool RefreshStillCurrent(int generation, long watchSession)
    {
        return IsCurrentInitialization(generation) && watchSession == _workspaceWatch.WatchSession;
    }

    private IReadOnlyCollection<string> ProjectDirectories(string solutionPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (Directory.Exists(solutionPath))
        {
            return SolutionProjectDiscovery.GetProjectDirectories(null, solutionPath);
        }

        if (_solution == null)
        {
            return null;
        }

        return SolutionProjectDiscovery.GetProjectDirectories(_solution, solutionPath);
    }

    private async Task OnBranchChangedAsync(string newBranch)
    {
        try
        {
            Log(logger =>
            {
                logger.Info($"Branch switched to: '{newBranch}'. Re-seeding workspace watches...");
                return Task.CompletedTask;
            });

            await RefreshWorkspacePathsAsync();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _scheduler.Schedule(ct => CodeSceneToolWindow.UpdateViewAsync());
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

    private string GetOpenFolderPath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_solution == null)
        {
            return null;
        }

        _solution.GetSolutionInfo(out var directory, out var solutionFile, out _);
        if (!string.IsNullOrEmpty(solutionFile))
        {
            return null;
        }

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        return directory;
    }

    private bool IsCurrentInitialization(int generation)
    {
        lock (_initGate)
        {
            return generation == _initGeneration;
        }
    }

    private void ResumeBufferReviewsIfCurrent(int generation)
    {
        lock (_initGate)
        {
            if (generation != _initGeneration)
            {
                return;
            }

            _reviewPipeline?.ResumeBufferReviews();
        }
    }

    private void ResetWorkspaceForInitialization()
    {
        _branchWatcher?.Dispose();
        _branchWatcher = null;
        _workspaceWatch?.StopAll();
        _reviewPipeline?.Reset();
        _reviewPipeline?.SetActiveRepos(Array.Empty<string>());
    }

    private void ClearAnalysisCaches()
    {
        new DeltaCacheService().Clear();
        new ReviewCacheService().Clear();
        new AceRefactorableFunctionsCacheService().Clear();
        _codeHealthMonitorNotifier?.Clear();
    }
}
