using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using Codescene.VSExtension.Core.IntegrationTests.TestImplementations;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    public abstract class BaseIntegrationTests
    {
        // These are mocks of services and handlers that are only implemented in Codescene.VSExtension.VS2022.
        // To avoid that dependency for the integration tests, and focus only on .Core, we mock them here.
        protected Mock<ICacheStorageService> mockCacheStorageService;
        protected Mock<IErrorListWindowHandler> mockErrorListWindowHandler;
        protected Mock<IExtensionMetadataProvider> mockExtensionMetadataProvider;
        protected Mock<IGitService> mockGitService;
        protected Mock<ILogger> mockLogger;
        protected Mock<ISettingsProvider> mockSettingsProvider;

        protected CompositionContainer _container;

        /// <summary>
        /// Initializes the DI container with real implementations from .Core project.
        /// </summary>
        public virtual void Initialize()
        {
            var coreCatalog = new AssemblyCatalog(typeof(ICliExecutor).Assembly);
            var testCatalog = new AssemblyCatalog(typeof(BaseIntegrationTests).Assembly);
            var catalog = new AggregateCatalog(testCatalog, coreCatalog);
            _container = new CompositionContainer(catalog);

            mockCacheStorageService = ((TestCacheStorageService)_container.GetExportedValue<ICacheStorageService>()).Mock;
            mockErrorListWindowHandler = ((TestErrorListWindowHandler)_container.GetExportedValue<IErrorListWindowHandler>()).Mock;
            mockExtensionMetadataProvider = ((TestExtensionMetadataProvider)_container.GetExportedValue<IExtensionMetadataProvider>()).Mock;
            mockGitService = ((TestGitService)_container.GetExportedValue<IGitService>()).Mock;
            mockLogger = ((TestLogger)_container.GetExportedValue<ILogger>()).Mock;
            mockSettingsProvider = ((TestSettingsProvider)_container.GetExportedValue<ISettingsProvider>()).Mock;
        }

        /// <summary>
        /// Returns a service from the DI container
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T GetService<T>()
        {
            return _container.GetExportedValue<T>();
        }

        /// <summary>
        /// Overrides a service with a mock instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="instance"></param>
        protected void MockService<T, U>(U instance)
            where U : T
        {
            _container.ComposeExportedValue<T>(instance);
        }
    }
}
