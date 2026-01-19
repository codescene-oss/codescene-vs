using Codescene.VSExtension.Core.Application.Services.Cli;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    [TestClass]
    public class CliFileTest
    {
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
    }
}
