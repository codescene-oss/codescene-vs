using Codescene.VSExtension.Core.Application.Services.Authentication;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.MDFileHandler;
using Microsoft.Extensions.DependencyInjection;

namespace Codescene.VSExtension.Core
{
    public static class ApplicationServiceRegistration
    {
        public static void AddApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<ICliExecuter, CliExecuter>();
            services.AddSingleton<ICliCommandProvider, CliCommandProvider>();
            services.AddSingleton<IModelMapper, ModelMapper>();
            services.AddSingleton<ICliDownloader, CliDownloader>();
            services.AddSingleton<ICliSettingsProvider, CliSettingsProvider>();
            services.AddSingleton<ICliFileChecker, CliFileChecker>();
            services.AddSingleton<IMDFileHandler, MDFileHandler>();
        }
    }
}
