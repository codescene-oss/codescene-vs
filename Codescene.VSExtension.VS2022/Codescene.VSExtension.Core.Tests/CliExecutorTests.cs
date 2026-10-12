// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliExecutorTests
    {
        private const string TestCachePath = "/test/cache/path";

        private Mock<ILogger> _mockLogger;
        private Mock<IIdeServerClient> _mockClient;
        private Mock<ICacheStorageService> _mockCacheStorage;
        private Mock<ISettingsProvider> _mockSettingsProvider;
        private Mock<ITelemetryManager> _mockTelemetryManager;
        private Lazy<ITelemetryManager> _lazyTelemetryManager;
        private CliExecutor _cliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockClient = new Mock<IIdeServerClient>();
            _mockCacheStorage = new Mock<ICacheStorageService>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockTelemetryManager = new Mock<ITelemetryManager>();
            _lazyTelemetryManager = new Lazy<ITelemetryManager>(() => _mockTelemetryManager.Object);
            _mockCacheStorage.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(TestCachePath);
            _cliExecutor = new CliExecutor(
                _mockLogger.Object,
                _mockClient.Object,
                _mockCacheStorage.Object,
                _mockSettingsProvider.Object,
                _lazyTelemetryManager);
        }

        [TestMethod]
        public async Task PreflightAsync_WithValidResponse_ReturnsPreFlightResponseModel()
        {
            var expectedPreflight = new PreFlightResponseModel { Version = 1.0m, FileTypes = new[] { ".cs", ".js" } };
            _mockClient.Setup(x => x.PreflightAsync(true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPreflight);

            var result = await _cliExecutor.PreflightAsync(force: true);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedPreflight.Version, result.Version);
            Assert.IsTrue(result.FileTypes.SequenceEqual(expectedPreflight.FileTypes));
        }

        [TestMethod]
        public async Task PreflightAsync_WhenClientThrowsException_ReturnsNull()
        {
            _mockClient.Setup(x => x.PreflightAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.PreflightAsync();

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Preflight failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task PreflightAsync_WithForceFalse_UsesCorrectFlag()
        {
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            _mockClient.Setup(x => x.PreflightAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(preflight);

            var result = await _cliExecutor.PreflightAsync(force: false);

            Assert.IsNotNull(result);
            _mockClient.Verify(x => x.PreflightAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(null, _mockClient.Object, _mockCacheStorage.Object, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(_mockLogger.Object, null, _mockCacheStorage.Object, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullTelemetryManagerLazy_DoesNotThrow()
        {
            var executor = new CliExecutor(
                _mockLogger.Object,
                _mockClient.Object,
                _mockCacheStorage.Object,
                _mockSettingsProvider.Object);

            Assert.IsNotNull(executor);
        }
    }
}
