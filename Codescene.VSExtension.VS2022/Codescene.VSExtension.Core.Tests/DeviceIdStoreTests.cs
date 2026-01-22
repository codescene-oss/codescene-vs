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
        public void GetDeviceId_ReturnsValueFromCli()
        {
            // Arrange
            var expectedDeviceId = "device-abc-123";
            _mockCliExecutor.Setup(x => x.GetDeviceId()).Returns(expectedDeviceId);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = store.GetDeviceId();

            // Assert
            Assert.AreEqual(expectedDeviceId, result);
            _mockCliExecutor.Verify(x => x.GetDeviceId(), Times.Once);
        }

        [TestMethod]
        public void GetDeviceId_CachesResultOnSubsequentCalls()
        {
            // Arrange
            var expectedDeviceId = "cached-device-id";
            _mockCliExecutor.Setup(x => x.GetDeviceId()).Returns(expectedDeviceId);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act - call twice
            var result1 = store.GetDeviceId();
            var result2 = store.GetDeviceId();

            // Assert - CLI should only be called once due to caching
            Assert.AreEqual(expectedDeviceId, result1);
            Assert.AreEqual(expectedDeviceId, result2);
            _mockCliExecutor.Verify(x => x.GetDeviceId(), Times.Once);
        }

        [TestMethod]
        public void GetDeviceId_WhenExceptionThrown_LogsWarningAndReturnsEmptyString()
        {
            // Arrange
            var expectedException = new Exception("CLI failed");
            _mockCliExecutor.Setup(x => x.GetDeviceId()).Throws(expectedException);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = store.GetDeviceId();

            // Assert
            Assert.AreEqual("", result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Failed to fetch device ID"))), Times.Once);
        }

        [TestMethod]
        public void GetDeviceId_AfterException_RetriesOnSubsequentCalls()
        {
            // Arrange
            // Note: The implementation sets _deviceId = "" on exception, but the check
            // is !string.IsNullOrEmpty(_deviceId), so empty string is NOT cached
            _mockCliExecutor.Setup(x => x.GetDeviceId()).Throws(new Exception("CLI failed"));
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act - call twice
            var result1 = store.GetDeviceId();
            var result2 = store.GetDeviceId();

            // Assert - CLI is called each time since empty string is not cached
            Assert.AreEqual("", result1);
            Assert.AreEqual("", result2);
            _mockCliExecutor.Verify(x => x.GetDeviceId(), Times.Exactly(2));
        }

        [TestMethod]
        public void GetDeviceId_WhenCliReturnsNull_ReturnsEmptyString()
        {
            // Arrange
            _mockCliExecutor.Setup(x => x.GetDeviceId()).Returns((string)null);
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = store.GetDeviceId();

            // Assert
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void GetDeviceId_WhenCliReturnsEmptyString_ReturnsEmptyString()
        {
            // Arrange
            _mockCliExecutor.Setup(x => x.GetDeviceId()).Returns("");
            var store = new DeviceIdStore(_mockLogger.Object, _mockCliExecutor.Object);

            // Act
            var result = store.GetDeviceId();

            // Assert
            Assert.AreEqual("", result);
        }
    }
}
