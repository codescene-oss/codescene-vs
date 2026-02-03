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

        /// <summary>
        /// Asserts that the CLI file is downloaded, and is present in the expected location.
        /// </summary>
        [TestMethod]
        public void Cli_FileExists()
        {
            // Arrange
            var cliSettingsProvider = new CliSettingsProvider();
            // Act
            var cliFilePath = cliSettingsProvider.CliFileFullPath;
            // Assert
            Assert.IsTrue(File.Exists(cliFilePath), $"CLI file does not exist at path: {cliFilePath}");

        }

        /// <summary>
        /// Validates that the CLI is up and running and is callable via the real CliExecutor implementation.
        /// </summary>
        [TestMethod]
        public void Cli_VersionCheck_ShouldReturnCorrectVersion()
        {
            // Arrange
            var cliExecutor = GetService<ICliExecutor>();
            // Act
            var versionOutput = cliExecutor.GetFileVersion().Trim();
            // Assert
            Assert.AreEqual(_settingsProvider.RequiredDevToolVersion, versionOutput);
        }

        /// <summary>
        /// Tests mocking strategy, and Test_Mocking2 validates that mocking the same service in parallell works as expected.
        /// </summary>
        [TestMethod]
        public void Test_Mocking()
        {
            // Arrange
            MockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation())
                .Returns("/mocked/location");
            var cacheStorageService = GetService<ICacheStorageService>();

            // Act
            var location = cacheStorageService.GetSolutionReviewCacheLocation();

            // Assert
            Assert.AreEqual("/mocked/location", location);
        }

        [TestMethod]
        public void Test_Mocking2()
        {
            // Arrange
            MockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation())
                .Returns("/mocked/location2");
            var cacheStorageService = GetService<ICacheStorageService>();

            // Act
            var location = cacheStorageService.GetSolutionReviewCacheLocation();

            // Assert
            Assert.AreEqual("/mocked/location2", location);
        }
    }
}
