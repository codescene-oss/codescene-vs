// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    [TestClass]
    public class IntegrationSetupTests : BaseIntegrationTests
    {
        private ICliSettingsProvider _settingsProvider;

        [TestInitialize]
        public override void Initialize()
        {
            base.Initialize();
            _settingsProvider = new CliSettingsProvider();
        }

        [TestMethod]
        public void Cli_DistributionExists()
        {
            Assert.IsTrue(File.Exists(_settingsProvider.JarFullPath), $"CLI jar does not exist at path: {_settingsProvider.JarFullPath}");
            Assert.IsTrue(File.Exists(_settingsProvider.JavaExeFullPath), $"JRE java.exe does not exist at path: {_settingsProvider.JavaExeFullPath}");
        }

        [TestMethod]
        public async Task Cli_VersionCheck_ShouldReturnCorrectVersion()
        {
            var host = GetService<IIdeServerHost>();
            await host.StartAsync();
            Assert.AreEqual(_settingsProvider.RequiredDevToolVersion, host.Metadata.Sha);
        }

        [TestMethod]
        public void Test_Mocking()
        {
            mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation())
                .Returns("/mocked/location");
            var cacheStorageService = GetService<ICacheStorageService>();

            var location = cacheStorageService.GetSolutionReviewCacheLocation();

            Assert.AreEqual("/mocked/location", location);
        }

        [TestMethod]
        public void Test_Mocking2()
        {
            mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation())
                .Returns("/mocked/location2");
            var cacheStorageService = GetService<ICacheStorageService>();

            var location = cacheStorageService.GetSolutionReviewCacheLocation();

            Assert.AreEqual("/mocked/location2", location);
        }
    }
}
