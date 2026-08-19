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
        private Mock<ICliSettingsProvider> _mockCliSettingsProvider;
        private CliFileChecker _fileChecker;
        private string _tempDir;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCliSettingsProvider = new Mock<ICliSettingsProvider>();
            _fileChecker = new CliFileChecker(_mockLogger.Object, _mockCliSettingsProvider.Object);
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [TestMethod]
        public async Task CheckAsync_DistributionMissing_ReturnsFalse()
        {
            _mockCliSettingsProvider.Setup(x => x.DistributionFullPath).Returns(_tempDir);
            _mockCliSettingsProvider.Setup(x => x.JavaExeFullPath).Returns(Path.Combine(_tempDir, "missing-java.exe"));
            _mockCliSettingsProvider.Setup(x => x.JarFullPath).Returns(Path.Combine(_tempDir, "missing.jar"));

            var result = await _fileChecker.CheckAsync();

            Assert.IsFalse(result);
            _mockLogger.Verify(
                l => l.Error(It.Is<string>(s => s.Contains("not found") && s.Contains("bundled")), It.IsAny<FileNotFoundException>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CheckAsync_DistributionPresent_ReturnsTrue()
        {
            var java = Path.Combine(_tempDir, "java.exe");
            var jar = Path.Combine(_tempDir, "cs-ide.jar");
            File.WriteAllText(java, "java");
            File.WriteAllText(jar, "jar");
            _mockCliSettingsProvider.Setup(x => x.DistributionFullPath).Returns(_tempDir);
            _mockCliSettingsProvider.Setup(x => x.JavaExeFullPath).Returns(java);
            _mockCliSettingsProvider.Setup(x => x.JarFullPath).Returns(jar);

            var result = await _fileChecker.CheckAsync();

            Assert.IsTrue(result);
        }
    }
}
