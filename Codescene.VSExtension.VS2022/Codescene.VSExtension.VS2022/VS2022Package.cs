global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.VS2022.ToolWindows.Markdown;
using Codescene.VSExtension.VS2022.ToolWindows.Problems;
using Codescene.VSExtension.VS2022.ToolWindows.Status;
using Codescene.VSExtension.VS2022.ToolWindows.UserControlWindow;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022;
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
public sealed class VS2022Package : AsyncPackage
{
    [Import]
    private readonly ICliFileChecker _cliFileChecker;

    //private PackageCommandManager commandManager;
    public static async Task<T> GetServiceAsync<T>()
    {
        var serviceProvider = await VS.GetServiceAsync<SToolkitServiceProvider<VS2022Package>, IToolkitServiceProvider<VS2022Package>>();
        if (serviceProvider == null)
        {
            throw new ArgumentNullException("serviceProvider");
        }
        return (T)serviceProvider.GetRequiredService(typeof(T));
    }

    //protected override void InitializeServices(IServiceCollection services)
    //{
    //    RegisterServices(services);
    //    services.RegisterCommands(ServiceLifetime.Singleton);
    //}
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        try
        {
            await base.InitializeAsync(cancellationToken, progress);

            //Check CLI file            
            var componentModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
            componentModel.DefaultCompositionService.SatisfyImportsOnce(this);
            await _cliFileChecker.Check();
            //this.RegisterToolWindows();
            //await InitOnUIThreadAsync();
        }
        catch (Exception ex)
        {
            var e = ex.Message;
            throw;
        }
    }


    //void RegisterServices(IServiceCollection services)
    //{
    //    services.AddApplicationServices();
    //    services.AddSingleton<IIssuesHandler, IssuesHandler>();
    //    services.AddSingleton<ILogger, Logger>();
    //    services.AddSingleton<IPersistenceAuthDataProvider, CredentialManagerProvider>();
    //}
    //private Task InitOnUIThreadAsync()
    //{
    //    commandManager = new PackageCommandManager(
    //        ServiceProvider.GetRequiredService<IMenuCommandService>(),
    //        ServiceProvider.GetRequiredService<IAuthenticationService>(),
    //        ServiceProvider.GetRequiredService<ILogger>());

    //    commandManager.Initialize(ShowOptionPage);
    //    return Task.CompletedTask;
    //}
}