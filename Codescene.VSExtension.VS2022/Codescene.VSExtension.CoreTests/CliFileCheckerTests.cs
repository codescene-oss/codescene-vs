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

        private void SetupCliPathMock()
        {
            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
        }

        private void SetupSuccessfulDownload()
        {
            _mockCliDownloader.Setup(x => x.DownloadAsync()).Returns(Task.CompletedTask);
        }

        private void SetupVersionMocks(string requiredVersion, string currentVersion)
        {
            _mockCliSettingsProvider.Setup(x => x.RequiredDevToolVersion).Returns(requiredVersion);
            _mockCliExecutor.Setup(x => x.GetFileVersion()).Returns(currentVersion);
        }

        private void CreateTempFile()
        {
            File.WriteAllText(_tempFilePath, "dummy content");
        }

        private void VerifyErrorLogged()
        {
            _mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Failed to set up")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task Check_FileDoesNotExist_ShouldDownloadFile()
        {
            SetupCliPathMock();
            SetupSuccessfulDownload();

            await _fileChecker.Check();

            _mockCliDownloader.Verify(x => x.DownloadAsync(), Times.Once);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Setting up CodeScene"))), Times.Once);
        }

        [TestMethod]
        public async Task Check_FileExistsAndVersionMatches_ShouldNotDownload()
        {
            CreateTempFile();
            SetupCliPathMock();
            SetupVersionMocks("abc123", "abc123");

            await _fileChecker.Check();

            _mockCliDownloader.Verify(x => x.DownloadAsync(), Times.Never);
        }

        [TestMethod]
        public async Task Check_FileExistsButVersionMismatch_ShouldDeleteAndDownload()
        {
            CreateTempFile();
            SetupCliPathMock();
            SetupVersionMocks("newversion123", "oldversion456");
            SetupSuccessfulDownload();

            await _fileChecker.Check();

            _mockCliDownloader.Verify(x => x.DownloadAsync(), Times.Once);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Updating CodeScene tool"))), Times.Once);
            Assert.IsFalse(File.Exists(_tempFilePath), "Old CLI file should be deleted");
        }

        [TestMethod]
        public async Task Check_WhenDownloadThrows_ShouldLogError()
        {
            SetupCliPathMock();
            _mockCliDownloader.Setup(x => x.DownloadAsync()).ThrowsAsync(new Exception("Download failed"));

            await _fileChecker.Check();

            VerifyErrorLogged();
        }

        [TestMethod]
        public async Task Check_WhenGetFileVersionThrows_ShouldLogError()
        {
            CreateTempFile();
            SetupCliPathMock();
            _mockCliExecutor.Setup(x => x.GetFileVersion()).Throws(new Exception("Version check failed"));

            await _fileChecker.Check();

            VerifyErrorLogged();
        }

        [TestMethod]
        public async Task Check_SuccessfulDownload_ShouldLogCompletionTime()
        {
            SetupCliPathMock();
            SetupSuccessfulDownload();

            await _fileChecker.Check();

            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("setup completed"))), Times.Once);
        }

        [TestMethod]
        public async Task Check_SuccessfulUpdate_ShouldLogToolUpdated()
        {
            CreateTempFile();
            SetupCliPathMock();
            SetupVersionMocks("new", "old");
            SetupSuccessfulDownload();

            await _fileChecker.Check();

            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("tool updated"))), Times.Once);
        }
    }
}
