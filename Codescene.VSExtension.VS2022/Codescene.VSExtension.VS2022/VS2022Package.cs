using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.VS2022.Application.ErrorHandling;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Codescene.VSExtension.VS2022.Listeners;
using Codescene.VSExtension.VS2022.ToolWindows.Markdown;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CodeSceneConstants = Codescene.VSExtension.Core.Application.Services.Util.Constants;
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

            SendTelemetry(CodeSceneConstants.Telemetry.ON_ACTIVATE_EXTENSION);
        }
        catch (Exception e)
        {
            // Note: we may not be able to report every failure via telemetry
            // (e.g. if the extension hasn't fully loaded or the CLI hasn't been downloaded yet).

            System.Diagnostics.Debug.Fail($"VS2022Package.InitializeAsync failed for CodeScene Extension: {e}");
            SendTelemetry(CodeSceneConstants.Telemetry.ON_ACTIVATE_EXTENSION_ERROR);
            RunPreflight();
        }
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

    async Task InitializeSolutionEventsHandlerAsync()
    {
        await new SolutionEventsHandler().Initialize(this);
    }

    async Task HideOpenedWindowsAsync()
    {
        await AceToolWindow.HideAsync();
    }

    async Task<T> GetServiceAsync<T>()
    {
        if (await GetServiceAsync(typeof(SComponentModel)) is IComponentModel componentModel)
        {
            return componentModel.DefaultExportProvider.GetExportedValue<T>();
        }

        throw new Exception($"Can not find component {nameof(T)}");
    }

    async Task InitializeLoggerPaneAsync()
    {
        var logPane = await GetServiceAsync<OutputPaneManager>();
        await logPane.InitializeAsync();
    }

    async Task CheckCliFileAsync()
    {
        var cliFileChecker = await GetServiceAsync<ICliFileChecker>();
        await cliFileChecker.Check();
    }

    async Task SubscribeOnActiveWindowChangeAsync()
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

        if (await GetServiceAsync(typeof(SDTE)) is DTE dte)
        {
            var handler = await GetServiceAsync<OnActiveWindowChangeHandler>();
            dte.Events.WindowEvents.WindowActivated += handler.Handle;
        }
    }
}
