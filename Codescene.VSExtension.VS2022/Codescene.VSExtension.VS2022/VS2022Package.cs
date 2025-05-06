using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
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
using Task = System.Threading.Tasks.Task;

namespace Codescene.VSExtension.VS2022;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuids.CodesceneExtensionString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]

[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene", "General", 0, 0, true, SupportsProfiles = true)]

[ProvideToolWindow(typeof(MarkdownWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
[ProvideToolWindow(typeof(AceToolWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer, Transient = true)]
[ProvideToolWindow(typeof(CodeHealthToolWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.ServerExplorer, Transient = true)]
public sealed class VS2022Package : ToolkitPackage
{
    public static VS2022Package Instance { get; private set; }

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        Instance = this;

        // Tool windows
        this.RegisterToolWindows();

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // Commands
        await this.RegisterCommandsAsync();

        // Events
        await RegisterEventsAsync();

        // Cli file
        await CheckCliFileAsync();

        // Check active document
        await CheckActiveOpenedDocumentAsync();

        // Subscribe on active document change event
        await SubscribeOnActiveWindowChangeAsync();
    }

    async Task<T> GetServiceAsync<T>()
    {
        if (await GetServiceAsync(typeof(SComponentModel)) is IComponentModel componentModel)
        {
            return componentModel.DefaultExportProvider.GetExportedValue<T>();
        }

        throw new Exception($"Can not find component {nameof(T)}");
    }

    async Task RegisterEventsAsync()
    {
        var eventManager = await GetServiceAsync<ExtensionEventsManager>();
        eventManager.RegisterEvents();
    }

    async Task CheckCliFileAsync()
    {
        var cliFileChecker = await GetServiceAsync<ICliFileChecker>();
        await cliFileChecker.Check();
    }

    async Task CheckActiveOpenedDocumentAsync()
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

        if (await GetServiceAsync(typeof(SDTE)) is DTE dte)
        {
            Document activeDocument = dte.ActiveDocument;
            if (activeDocument != null)
            {
                var path = activeDocument.FullName;
                var handler = await GetServiceAsync<OnStartExtensionActiveDocumentHandler>();
                handler.Handle(path);
            }
        }
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
