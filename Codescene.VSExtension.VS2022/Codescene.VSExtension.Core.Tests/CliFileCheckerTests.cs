// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliFileCheckerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<ICliExecutor> _mockCliExecutor;
        private Mock<ICliSettingsProvider> _mockCliSettingsProvider;
        private CliFileChecker _fileChecker;

        private string _tempFilePath;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCliExecutor = new Mock<ICliExecutor>();
            _mockCliSettingsProvider = new Mock<ICliSettingsProvider>();

            _fileChecker = new CliFileChecker(
                _mockLogger.Object,
                _mockCliExecutor.Object,
                _mockCliSettingsProvider.Object);

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
        public async Task CheckAsync_FileDoesNotExist_ShouldLogErrorAndReturnFalse()
        {
            SetupCliPathMock();

            var result = await _fileChecker.CheckAsync();

            Assert.IsFalse(result);
            _mockLogger.Verify(
                l => l.Error(
                It.Is<string>(s => s.Contains("not found") && s.Contains("bundled")),
                It.IsAny<FileNotFoundException>()), Times.Once);
        }

        [TestMethod]
        public async Task CheckAsync_FileExists_ShouldLogVersionAndReturnTrue()
        {
            CreateTempFile();
            SetupCliPathMock();
            SetupVersionMock("abc123");

            var result = await _fileChecker.CheckAsync();

            Assert.IsTrue(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Using CLI version") && s.Contains("abc123"))), Times.Once);
        }

        [TestMethod]
        public async Task CheckAsync_FileExistsButVersionCheckReturnsEmpty_ShouldLogWarningAndReturnFalse()
        {
            CreateTempFile();
            SetupCliPathMock();
            SetupVersionMock(string.Empty);

            var result = await _fileChecker.CheckAsync();

            Assert.IsFalse(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Could not determine CLI version"))), Times.Once);
        }

        [TestMethod]
        public async Task CheckAsync_FileExistsButVersionCheckReturnsNull_ShouldLogWarningAndReturnFalse()
        {
            CreateTempFile();
            SetupCliPathMock();
            SetupVersionMock(null);

            var result = await _fileChecker.CheckAsync();

            Assert.IsFalse(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Could not determine CLI version"))), Times.Once);
        }

        [TestMethod]
        public async Task CheckAsync_WhenGetFileVersionThrows_ShouldLogErrorAndReturnFalse()
        {
            CreateTempFile();
            SetupCliPathMock();
            _mockCliExecutor.Setup(x => x.GetFileVersionAsync()).ThrowsAsync(new Exception("Version check failed"));

            var result = await _fileChecker.CheckAsync();

            Assert.IsFalse(result);
            _mockLogger.Verify(
                l => l.Error(
                It.Is<string>(s => s.Contains("Failed to check")),
                It.IsAny<Exception>()), Times.Once);
        }

        private void SetupCliPathMock()
        {
            _mockCliSettingsProvider.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
        }

        private void SetupVersionMock(string version)
        {
            _mockCliExecutor.Setup(x => x.GetFileVersionAsync()).ReturnsAsync(version);
        }

        private void CreateTempFile()
        {
            File.WriteAllText(_tempFilePath, "dummy content");
        }
    }
}
