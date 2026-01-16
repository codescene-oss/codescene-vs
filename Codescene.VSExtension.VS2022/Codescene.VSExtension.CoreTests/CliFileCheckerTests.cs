using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CliFileCheckerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<ICliExecutor> _mockCliExecutor;
        private Mock<ICliSettingsProvider> _mockCliSettingsProvider;
        private Mock<ICliDownloader> _mockCliDownloader;
        private CliFileChecker _fileChecker;

        private string _tempFilePath;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCliExecutor = new Mock<ICliExecutor>();
            _mockCliSettingsProvider = new Mock<ICliSettingsProvider>();
            _mockCliDownloader = new Mock<ICliDownloader>();

            _fileChecker = new CliFileChecker(
                _mockLogger.Object,
                _mockCliExecutor.Object,
                _mockCliSettingsProvider.Object,
                _mockCliDownloader.Object);

            // Create a temp file path for testing
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up temp file if it exists
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public async Task Check_FileDoesNotExist_ShouldDownloadFile()
        {
            // Arrange
            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _mockCliDownloader.Setup(x => x.DownloadAsync()).Returns(Task.CompletedTask);

            // Act
            await _fileChecker.Check();

            // Assert
            _mockCliDownloader.Verify(x => x.DownloadAsync(), Times.Once);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Setting up CodeScene"))), Times.Once);
        }

        [TestMethod]
        public async Task Check_FileExistsAndVersionMatches_ShouldNotDownload()
        {
            // Arrange
            var requiredVersion = "abc123";
            File.WriteAllText(_tempFilePath, "dummy content");

            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _mockCliSettingsProvider.Setup(x => x.RequiredDevToolVersion).Returns(requiredVersion);
            _mockCliExecutor.Setup(x => x.GetFileVersion()).Returns(requiredVersion);

            // Act
            await _fileChecker.Check();

            // Assert
            _mockCliDownloader.Verify(x => x.DownloadAsync(), Times.Never);
        }

        [TestMethod]
        public async Task Check_FileExistsButVersionMismatch_ShouldDeleteAndDownload()
        {
            // Arrange
            var requiredVersion = "newversion123";
            var currentVersion = "oldversion456";
            File.WriteAllText(_tempFilePath, "dummy content");

            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _mockCliSettingsProvider.Setup(x => x.RequiredDevToolVersion).Returns(requiredVersion);
            _mockCliExecutor.Setup(x => x.GetFileVersion()).Returns(currentVersion);
            _mockCliDownloader.Setup(x => x.DownloadAsync()).Returns(Task.CompletedTask);

            // Act
            await _fileChecker.Check();

            // Assert
            _mockCliDownloader.Verify(x => x.DownloadAsync(), Times.Once);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Updating CodeScene tool"))), Times.Once);
            Assert.IsFalse(File.Exists(_tempFilePath), "Old CLI file should be deleted");
        }

        [TestMethod]
        public async Task Check_WhenDownloadThrows_ShouldLogError()
        {
            // Arrange
            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _mockCliDownloader.Setup(x => x.DownloadAsync())
                .ThrowsAsync(new Exception("Download failed"));

            // Act
            await _fileChecker.Check();

            // Assert
            _mockLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Failed to set up")),
                It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task Check_WhenGetFileVersionThrows_ShouldLogError()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "dummy content");

            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _mockCliExecutor.Setup(x => x.GetFileVersion()).Throws(new Exception("Version check failed"));

            // Act
            await _fileChecker.Check();

            // Assert
            _mockLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Failed to set up")),
                It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task Check_SuccessfulDownload_ShouldLogCompletionTime()
        {
            // Arrange
            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _mockCliDownloader.Setup(x => x.DownloadAsync()).Returns(Task.CompletedTask);

            // Act
            await _fileChecker.Check();

            // Assert
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("setup completed"))), Times.Once);
        }

        [TestMethod]
        public async Task Check_SuccessfulUpdate_ShouldLogToolUpdated()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "dummy content");

            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _mockCliSettingsProvider.Setup(x => x.RequiredDevToolVersion).Returns("new");
            _mockCliExecutor.Setup(x => x.GetFileVersion()).Returns("old");
            _mockCliDownloader.Setup(x => x.DownloadAsync()).Returns(Task.CompletedTask);

            // Act
            await _fileChecker.Check();

            // Assert
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("tool updated"))), Times.Once);
        }
    }
}
