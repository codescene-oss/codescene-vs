global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using CodesceneReeinventTest.Application;
using CodesceneReeinventTest.Application.Services.Authentication;
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
public sealed class CodesceneReeinventTestPackage : MicrosoftDIToolkitPackage<CodesceneReeinventTestPackage>
{
    protected override void InitializeServices(IServiceCollection services)
    {
        RegisterServices(services);
        services.RegisterCommands(ServiceLifetime.Singleton);
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
    }
}