using System;
using System.IO;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ProcessExecutorTests
    {
        private Mock<ICliSettingsProvider> _mockCliSettingsProvider;
        private ProcessExecutor _processExecutor;
        private string _tempFilePath;

        [TestInitialize]
        public void Setup()
        {
            _mockCliSettingsProvider = new Mock<ICliSettingsProvider>();
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public void Execute_FileDoesNotExist_ThrowsFileNotFoundException()
        {
            // Arrange
            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _processExecutor = new ProcessExecutor(_mockCliSettingsProvider.Object);

            // Act & Assert
            var exception = Assert.Throws<FileNotFoundException>(() =>
                _processExecutor.Execute("version --sha"));

            Assert.IsTrue(exception.Message.Contains("CodeScene CLI executable not found"));
            Assert.IsTrue(exception.Message.Contains("bundled with the extension"));
            Assert.AreEqual(_tempFilePath, exception.FileName);
        }

        [TestMethod]
        public void Execute_FileExists_FileNotFoundExceptionNotThrown()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "dummy executable content");
            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _processExecutor = new ProcessExecutor(_mockCliSettingsProvider.Object);

            // Act & Assert
            // The file existence check should pass (file exists), so FileNotFoundException should not be thrown
            // Note: Execution will likely fail with a different exception since this is not a real executable,
            // but the important part is that FileNotFoundException is not thrown at the file existence check stage
            try
            {
                _processExecutor.Execute("version --sha");

                // If we get here without FileNotFoundException, the file existence check passed
            }
            catch (FileNotFoundException ex)
            {
                // This should not happen if the file exists - the exception should not mention file not found
                Assert.Fail($"FileNotFoundException should not be thrown when file exists. Message: {ex.Message}");
            }
            catch (Exception)
            {
                // Any other exception is expected (e.g., process execution failure, invalid executable format)
                // The important thing is that FileNotFoundException was not thrown, meaning the file existence check passed
            }
        }
    }
}
