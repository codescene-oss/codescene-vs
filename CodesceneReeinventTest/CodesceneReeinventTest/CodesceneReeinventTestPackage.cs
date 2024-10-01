global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using CodesceneReeinventTest.Application;
using CodesceneReeinventTest.Application.Handlers;
using CodesceneReeinventTest.Application.Services.Authentication;
using CodesceneReeinventTest.Application.Services.FileReviewer;
using CodesceneReeinventTest.ToolWindows.Markdown;
using Community.VisualStudio.Toolkit.DependencyInjection.Microsoft;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using System.Threading;

namespace CodesceneReeinventTest;
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PackageGuids.CodesceneReeinventTestString)]
[ProvideToolWindow(typeof(ProblemsWindow.Pane))]
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene", "General", 0, 0, true, SupportsProfiles = true)]
[ProvideToolWindow(typeof(StatusWindow.Pane), Window = WindowGuids.SolutionExplorer, Style = VsDockStyle.Tabbed)]
[ProvideToolWindow(typeof(MarkdownWindow.Pane))]

public sealed class CodesceneReeinventTestPackage : MicrosoftDIToolkitPackage<CodesceneReeinventTestPackage>
{
    private static IServiceProvider _serviceProvider;
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
        this.RegisterToolWindows();
    }
    void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IIssuesHandler, IssuesHandler>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IMDFileHandler, MDFileHandler>();
        services.AddSingleton<IFileReviewer, FileReviewer>();
        services.AddSingleton<IModelMapper, ModelMapper>();
    }
}