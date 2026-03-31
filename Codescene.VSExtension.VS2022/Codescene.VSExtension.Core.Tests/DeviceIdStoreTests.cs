// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Telemetry;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class DeviceIdStoreTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<ICliExecutor> _mockCliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCliExecutor = new Mock<ICliExecutor>();
        }

        [TestMethod]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DeviceIdStore(null, _mockCliExecutor.Object));
        }

        [TestMethod]
        public void Constructor_NullCliExecutor_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DeviceIdStore(_mockLogger.Object, null));
        }

        [TestMethod]
        public async Task GetDeviceIdAsync_ReturnsValueFromCli()
        {
            // Arrange
            var expectedDeviceId = "device-abc-123";
            _mockCliExecutor.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync(expectedDeviceId);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = await store.GetDeviceIdAsync();

            // Assert
            Assert.AreEqual(expectedDeviceId, result);
            _mockCliExecutor.Verify(x => x.GetDeviceIdAsync(), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceIdAsync_CachesResultOnSubsequentCalls()
        {
            // Arrange
            var expectedDeviceId = "cached-device-id";
            _mockCliExecutor.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync(expectedDeviceId);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act - call twice
            var result1 = await store.GetDeviceIdAsync();
            var result2 = await store.GetDeviceIdAsync();

            // Assert - CLI should only be called once due to caching
            Assert.AreEqual(expectedDeviceId, result1);
            Assert.AreEqual(expectedDeviceId, result2);
            _mockCliExecutor.Verify(x => x.GetDeviceIdAsync(), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceIdAsync_WhenExceptionThrown_LogsWarningAndReturnsEmptyString()
        {
            // Arrange
            var expectedException = new Exception("CLI failed");
            _mockCliExecutor.Setup(x => x.GetDeviceIdAsync()).Throws(expectedException);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = await store.GetDeviceIdAsync();

            // Assert
            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Failed to fetch device ID"))), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceIdAsync_AfterException_RetriesOnSubsequentCalls()
        {
            // Arrange
            // Note: The implementation sets _deviceId = "" on exception, but the check
            // is !string.IsNullOrEmpty(_deviceId), so empty string is NOT cached
            _mockCliExecutor.Setup(x => x.GetDeviceIdAsync()).Throws(new Exception("CLI failed"));
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act - call twice
            var result1 = await store.GetDeviceIdAsync();
            var result2 = await store.GetDeviceIdAsync();

            // Assert - CLI is called each time since empty string is not cached
            Assert.AreEqual(string.Empty, result1);
            Assert.AreEqual(string.Empty, result2);
            _mockCliExecutor.Verify(x => x.GetDeviceIdAsync(), Times.Exactly(2));
        }

        [TestMethod]
        public async Task GetDeviceIdAsync_WhenCliReturnsNull_ReturnsEmptyString()
        {
            // Arrange
            _mockCliExecutor.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync((string)null);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = await store.GetDeviceIdAsync();

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public async Task GetDeviceIdAsync_WhenCliReturnsEmptyString_ReturnsEmptyString()
        {
            // Arrange
            _mockCliExecutor.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync(string.Empty);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = await store.GetDeviceIdAsync();

            // Assert
            Assert.AreEqual(string.Empty, result);
        }
    }
}
