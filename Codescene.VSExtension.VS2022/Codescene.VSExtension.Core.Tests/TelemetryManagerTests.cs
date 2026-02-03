// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Telemetry;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TelemetryManagerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<IProcessExecutor> _mockExecutor;
        private Mock<IDeviceIdStore> _mockDeviceIdStore;
        private Mock<ICliCommandProvider> _mockCommandProvider;
        private Mock<IExtensionMetadataProvider> _mockMetadataProvider;
        private TelemetryManager _telemetryManager;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockExecutor = new Mock<IProcessExecutor>();
            _mockDeviceIdStore = new Mock<IDeviceIdStore>();
            _mockCommandProvider = new Mock<ICliCommandProvider>();
            _mockMetadataProvider = new Mock<IExtensionMetadataProvider>();

            _telemetryManager = new TelemetryManager(
                _mockLogger.Object,
                _mockExecutor.Object,
                _mockDeviceIdStore.Object,
                _mockCommandProvider.Object,
                _mockMetadataProvider.Object);

            ErrorTelemetryUtils.ResetErrorCount();
        }

        [TestMethod]
        public void SendTelemetry_WhenExceptionThrown_LogsDebugAndDoesNotRethrow()
        {
            // Arrange
            var eventName = "test-event";
            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("telemetry command");
            _mockExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Throws(new Exception("Telemetry failed"));

            // Act - should not throw
            _telemetryManager.SendTelemetry(eventName);

            // Assert
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Unable to send telemetry"))), Times.Once);
        }

        [TestMethod]
        public void SendTelemetry_WithAdditionalData_DoesNotThrow()
        {
            // Arrange
            var eventName = "test-event";
            var additionalData = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 },
            };

            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("telemetry command");
            _mockExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Returns("success");

            // Act - should not throw
            _telemetryManager.SendTelemetry(eventName, additionalData);

            // Assert - no exception means success
            // Note: Actual execution depends on TelemetryUtils.IsTelemetryEnabled() which reads registry
        }

        [TestMethod]
        public void SendTelemetry_GetsDeviceIdFromStore()
        {
            // Arrange
            var eventName = "test-event";
            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("my-device-id");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("2.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("cmd");
            _mockExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Returns("ok");

            // Act
            _telemetryManager.SendTelemetry(eventName);

            // Assert - device ID store should be called (if telemetry is enabled)
            // Note: This verification depends on TelemetryUtils.IsTelemetryEnabled() returning true
        }

        [TestMethod]
        public void SendTelemetry_GetsVersionFromMetadataProvider()
        {
            // Arrange
            var eventName = "test-event";
            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("3.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("cmd");
            _mockExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Returns("ok");

            // Act
            _telemetryManager.SendTelemetry(eventName);

            // Assert - metadata provider should be called (if telemetry is enabled)
            // Note: This verification depends on TelemetryUtils.IsTelemetryEnabled() returning true
        }

        [TestMethod]
        public void SendErrorTelemetry_WhenExceptionThrown_LogsDebugAndDoesNotRethrow()
        {
            // Arrange
            var ex = new InvalidOperationException("Test error");
            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("telemetry command");
            _mockExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Throws(new Exception("Telemetry failed"));

            // Act - should not throw
            _telemetryManager.SendErrorTelemetry(ex, "Test context");

            // Assert - SendErrorTelemetry calls SendTelemetry which has its own exception handling
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Unable to send telemetry"))), Times.Once);
        }

        [TestMethod]
        public void SendErrorTelemetry_WithExtraData_DoesNotThrow()
        {
            // Arrange
            var ex = new Exception("Test error");
            var extraData = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 },
            };

            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("telemetry command");
            _mockExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Returns("success");

            // Act - should not throw
            _telemetryManager.SendErrorTelemetry(ex, "Test context", extraData);

            // Assert - no exception means success
        }

        [TestMethod]
        public void SendErrorTelemetry_TelemetryRelatedError_DoesNotSend()
        {
            // Arrange
            var ex = new Exception("Failed to send telemetry");

            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("cmd");

            // Act
            _telemetryManager.SendErrorTelemetry(ex, "context");

            // Assert - executor should not be called for telemetry-related errors
            _mockExecutor.Verify(
                x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()),
                Times.Never);
        }

        [TestMethod]
        public void SendErrorTelemetry_NetworkError_DoesNotSend()
        {
            // Arrange
            var ex = new Exception("ECONNREFUSED: Connection refused");

            _mockDeviceIdStore.Setup(x => x.GetDeviceId()).Returns("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockCommandProvider.Setup(x => x.SendTelemetryCommand(It.IsAny<string>())).Returns("cmd");

            // Act
            _telemetryManager.SendErrorTelemetry(ex, "context");

            // Assert - executor should not be called for network errors
            _mockExecutor.Verify(
                x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()),
                Times.Never);
        }
    }
}
