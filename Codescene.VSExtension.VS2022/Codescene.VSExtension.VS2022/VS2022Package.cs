// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.VS2022.Application.ErrorHandling;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Codescene.VSExtension.VS2022.Handlers;
using Codescene.VSExtension.VS2022.Listeners;
using Codescene.VSExtension.VS2022.ToolWindows.Markdown;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using CodeSceneConstants = Codescene.VSExtension.Core.Consts.Constants;
using Task = System.Threading.Tasks.Task;

namespace Codescene.VSExtension.VS2022;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuids.CodesceneExtensionString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]

[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene", "General", 0, 0, true, SupportsProfiles = true)]

[ProvideToolWindow(typeof(MarkdownWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
[ProvideToolWindow(typeof(AceToolWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer, Transient = true)]
[ProvideToolWindow(typeof(AceAcknowledgeToolWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer, Transient = true)]
[ProvideToolWindow(typeof(CodeSmellDocumentationWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer, Transient = true)]
[ProvideToolWindow(typeof(CodeSceneToolWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
public sealed class VS2022Package : ToolkitPackage
{
    public static VS2022Package Instance { get; private set; }

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        Instance = this;

        try
        {
            // Wait for shell to be fully initialized before proceeding
            await WaitForShellInitializationAsync(cancellationToken);

            // Logging
            await InitializeLoggerPaneAsync();

            // Tool windows
            this.RegisterToolWindows();

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Commands
            await this.RegisterCommandsAsync();

            // Cli file
            await CheckCliFileAsync();

            // Subscribe on active document change event
            await SubscribeOnActiveWindowChangeAsync();

            // Hide Windows
            await HideOpenedWindowsAsync();

            // Solution events handler
            await InitializeSolutionEventsHandlerAsync();

            // File-based cache storage for CLI
            await InitializeCacheStorageServiceAsync();

            // Initialize ACE state change handler
            await InitializeAceStateChangeHandlerAsync();

            // Initialize ACE at startup
            RunPreflight();

            SendTelemetry(CodeSceneConstants.Telemetry.ONACTIVATEEXTENSION);
        }
        catch (Exception e)
        {
            // Note: we may not be able to report every failure via telemetry
            // (e.g. if the extension hasn't fully loaded or the CLI hasn't been downloaded yet).
            System.Diagnostics.Debug.Fail($"VS2022Package.InitializeAsync failed for CodeScene Extension: {e}");
            SendTelemetry(CodeSceneConstants.Telemetry.ONACTIVATEEXTENSIONERROR);
        }
    }

    private async Task InitializeCacheStorageServiceAsync()
    {
        var cacheManager = await VS.GetMefServiceAsync<ICacheStorageService>();
        await cacheManager.InitializeAsync();
    }

    private async Task InitializeAceStateChangeHandlerAsync()
    {
        // Initialize the handler so it's ready to receive state change events
        // The handler subscribes to IAceStateService.StateChanged in its constructor
        await GetServiceAsync<AceStateChangeHandler>();
    }

    private void SendTelemetry(string eventName)
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(eventName);
        }).FireAndForget();
    }

    private void RunPreflight()
    {
        Task.Run(async () =>
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            preflightManager.RunPreflight(true);
        }).FireAndForget();
    }

    private async Task WaitForShellInitializationAsync(CancellationToken cancellationToken)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (VsShellUtilities.ShellIsInitialized)
        {
            return; // Shell is already initialized
        }

        // If not initialized, wait for it using a simple polling approach
        const int maxWaitTimeMs = 120000; // 120 seconds max wait (2 min)
        const int pollIntervalMs = 1000;   // Check every 1s
        int elapsedMs = 0;

        while (!VsShellUtilities.ShellIsInitialized && elapsedMs < maxWaitTimeMs)
        {
            await Task.Delay(pollIntervalMs, cancellationToken);
            elapsedMs += pollIntervalMs;
        }

        if (!VsShellUtilities.ShellIsInitialized)
        {
            throw new TimeoutException("Visual Studio shell failed to initialize within the expected time.");
        }
    }

    private async Task InitializeSolutionEventsHandlerAsync()
    {
        await new SolutionEventsHandler().InitializeAsync(this);
    }

    private async Task HideOpenedWindowsAsync()
    {
        await AceToolWindow.HideAsync();
    }

    private async Task<T> GetServiceAsync<T>()
    {
        if (await GetServiceAsync(typeof(SComponentModel)) is IComponentModel componentModel)
        {
            return componentModel.DefaultExportProvider.GetExportedValue<T>();
        }

        throw new Exception($"Can not find component {nameof(T)}");
    }

    private async Task InitializeLoggerPaneAsync()
    {
        var logPane = await GetServiceAsync<OutputPaneManager>();
        await logPane.InitializeAsync();
    }

    private async Task CheckCliFileAsync()
    {
        var cliFileChecker = await GetServiceAsync<ICliFileChecker>();
        cliFileChecker.Check();
    }

    private async Task SubscribeOnActiveWindowChangeAsync()
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

        if (await GetServiceAsync(typeof(SDTE)) is DTE dte)
        {
            var handler = await GetServiceAsync<OnActiveWindowChangeHandler>();
            dte.Events.WindowEvents.WindowActivated += handler.Handle;
        }
    }
}
