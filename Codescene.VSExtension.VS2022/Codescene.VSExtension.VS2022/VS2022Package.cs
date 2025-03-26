using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Codescene.VSExtension.VS2022.ToolWindows.Markdown;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace Codescene.VSExtension.VS2022;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuids.CodesceneExtensionString)]
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene", "General", 0, 0, true, SupportsProfiles = true)]
[ProvideToolWindow(typeof(MarkdownWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
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
    }

    async Task RegisterEventsAsync()
    {
        if (await GetServiceAsync(typeof(SComponentModel)) is IComponentModel componentModel)
        {
            var eventManager = componentModel.DefaultExportProvider.GetExportedValue<ExtensionEventsManager>();
            eventManager.RegisterEvents();
        }
    }

    async Task CheckCliFileAsync()
    {
        if (await GetServiceAsync(typeof(SComponentModel)) is not IComponentModel componentModel)
        {
            throw new ArgumentNullException(nameof(componentModel));
        }

        var cliFileChecker = componentModel.DefaultExportProvider.GetExportedValue<ICliFileChecker>();
        await cliFileChecker.Check();
    }
}
