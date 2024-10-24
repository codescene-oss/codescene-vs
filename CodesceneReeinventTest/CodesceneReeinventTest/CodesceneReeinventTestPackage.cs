global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using CodesceneReeinventTest.Application.ErrorHandling;
using CodesceneReeinventTest.Application.FileReviewer;
using CodesceneReeinventTest.Application.IssueHandler;
using CodesceneReeinventTest.Application.MDFileHandler;
using CodesceneReeinventTest.Commands;
using CodesceneReeinventTest.ToolWindows.Markdown;
using CodesceneReeinventTest.ToolWindows.Problems;
using CodesceneReeinventTest.ToolWindows.Status;
using CodesceneReeinventTest.ToolWindows.UserControlWindow;
using Community.VisualStudio.Toolkit.DependencyInjection.Microsoft;
using Core.Application.Services.Authentication;
using Core.Application.Services.ErrorHandling;
using Core.Application.Services.FileDownloader;
using Core.Application.Services.FileReviewer;
using Core.Application.Services.IssueHandler;
using Core.Application.Services.Mapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;

namespace CodesceneReeinventTest;
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageGuids.guidVsPackagePkgStringString)]
[ProvideToolWindow(typeof(ProblemsWindow.Pane))]
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene", "General", 0, 0, true, SupportsProfiles = true)]
[ProvideToolWindow(typeof(StatusWindow.Pane), Window = WindowGuids.SolutionExplorer, Style = VsDockStyle.Tabbed)]
[ProvideToolWindow(typeof(MarkdownWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
[ProvideToolWindow(typeof(UserControlWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
public sealed class CodesceneReeinventTestPackage : MicrosoftDIToolkitPackage<CodesceneReeinventTestPackage>
{
    private static IServiceProvider _serviceProvider;
    private PackageCommandManager commandManager;
    public static T GetService<T>()
    {
        if (_serviceProvider == null)
        {
            throw new ArgumentNullException("_serviceProvider");
        }

        return (T)_serviceProvider.GetService(typeof(T));
    }
    protected override void InitializeServices(IServiceCollection services)
    {
        RegisterServices(services);
        services.RegisterCommands(ServiceLifetime.Singleton);
        _serviceProvider = services.BuildServiceProvider();
    }
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await base.InitializeAsync(cancellationToken, progress);
        var componentModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
        componentModel.DefaultCompositionService.SatisfyImportsOnce(this);
        this.RegisterToolWindows();
        await InitOnUIThreadAsync();
    }
    void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IIssuesHandler, IssuesHandler>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IMDFileHandler, MDFileHandler>();
        services.AddSingleton<IFileReviewer, FileReviewer>();
        services.AddSingleton<IModelMapper, ModelMapper>();
        services.AddSingleton<IFileDownloader, FileDownloader>();
        services.AddSingleton<IErrorsHandler, ErrorsHandler>();
        services.AddSingleton<IPersistenceAuthDataProvider, PersistenceAuthDataProvider>();
    }
    private Task InitOnUIThreadAsync()
    {
        commandManager = new PackageCommandManager(
            _serviceProvider.GetService<IMenuCommandService>(),
            _serviceProvider.GetService<IAuthenticationService>(),
            _serviceProvider.GetService<IErrorsHandler>());

        commandManager.Initialize(
            ShowOptionPage);
        return Task.CompletedTask;
    }
}