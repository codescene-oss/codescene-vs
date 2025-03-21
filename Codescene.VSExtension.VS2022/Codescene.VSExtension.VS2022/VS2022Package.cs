using Codescene.VSExtension.Core.Application.Services.Cli;
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
//[ProvideProfile(typeof(OptionsProvider.GeneralOptions), "Codescene", "General", 0, 0, true)]

[ProvideToolWindow(typeof(MarkdownWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
//[ProvideToolWindowVisibility(typeof(MarkdownWindow.Pane), VSConstants.UICONTEXT.NoSolution_string)]
//[ProvideFileIcon(".abc", "KnownMonikers.Reference")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
//[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class VS2022Package : ToolkitPackage
{
    public static VS2022Package Instance { get; private set; }
    private readonly EventManager _eventManager = new();
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        Instance = this;

        // Tool windows
        this.RegisterToolWindows();

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // Commands
        await this.RegisterCommandsAsync();

        _eventManager.RegisterEvents();

        await CheckCliFileAsync();
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
