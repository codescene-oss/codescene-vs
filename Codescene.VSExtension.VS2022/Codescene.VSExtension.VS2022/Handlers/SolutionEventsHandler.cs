using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.Application.Git;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Threading.Tasks;

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

    /// <summary>
    /// Subscribes to solution events using the Visual Studio shell service.
    /// </summary>
    public async Task Initialize(IServiceProvider serviceProvider)
    {
        var isUiThread = ThreadHelper.CheckAccess();

        if (isUiThread)
        {
            _solution = (IVsSolution)serviceProvider.GetService(typeof(SVsSolution));
            _solution.AdviseSolutionEvents(this, out _cookie);
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
            if (string.IsNullOrEmpty(solutionPath)) return;

            _branchWatcher = new BranchWatcherService();
            _branchWatcher.StartWatching(solutionPath, async (newBranch) => await OnBranchChangedAsync(newBranch));
        });

        return VSConstants.S_OK;
    }

    public int OnBeforeCloseSolution(object pUnkReserved)
    {
        _branchWatcher?.Dispose();
        _branchWatcher = null;
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