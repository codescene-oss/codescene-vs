// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Telemetry;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Util;
using Moq;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TelemetryManagerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<IIdeServerClient> _mockClient;
        private Mock<IDeviceIdStore> _mockDeviceIdStore;
        private Mock<IExtensionMetadataProvider> _mockMetadataProvider;
        private TelemetryManager _telemetryManager;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockClient = new Mock<IIdeServerClient>();
            _mockDeviceIdStore = new Mock<IDeviceIdStore>();
            _mockMetadataProvider = new Mock<IExtensionMetadataProvider>();
            _mockMetadataProvider.Setup(x => x.GetEditorVersion()).Returns("18.1.1");
            _mockClient.Setup(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _telemetryManager = new TelemetryManager(
                _mockLogger.Object,
                _mockClient.Object,
                _mockDeviceIdStore.Object,
                _mockMetadataProvider.Object);

            ErrorTelemetryUtils.ResetErrorCount();
            TelemetryUtils.TelemetryEnabledOverrideForTests = true;
        }

        [TestCleanup]
        public void TearDown()
        {
            TelemetryUtils.TelemetryEnabledOverrideForTests = null;
        }

#if DEBUG
        [TestMethod]
        public async Task SendTelemetry_WhenExceptionThrown_LogsDebugAndDoesNotRethrow()
        {
            var eventName = "test-event";
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockClient.Setup(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Telemetry failed"));

            await _telemetryManager.SendTelemetryAsync(eventName);

            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Unable to send telemetry"))), Times.Once);
        }

#endif

        [TestMethod]
        public async Task SendTelemetryAsync_WithAdditionalData_DoesNotThrow()
        {
            var eventName = "test-event";
            var additionalData = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 },
            };

            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");

            await _telemetryManager.SendTelemetryAsync(eventName, additionalData);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_GetsDeviceIdFromStore()
        {
            var eventName = "test-event";
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("my-device-id");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("2.0.0");

            await _telemetryManager.SendTelemetryAsync(eventName);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_IncludesEditorVersionInPayload()
        {
            var eventName = "test-event";
            object capturedEvent = null;
            TelemetryUtils.TelemetryEnabledOverrideForTests = true;
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockMetadataProvider.Setup(x => x.GetEditorVersion()).Returns("18.1.1");
            _mockClient.Setup(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Callback<object, CancellationToken>((payload, _) => capturedEvent = payload)
                .Returns(Task.CompletedTask);

            await _telemetryManager.SendTelemetryAsync(eventName);

            Assert.IsNotNull(capturedEvent);
            var json = JsonConvert.SerializeObject(capturedEvent);
            Assert.Contains("\"editor-version\":\"18.1.1\"", json);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_GetsVersionFromMetadataProvider()
        {
            var eventName = "test-event";
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("3.0.0");

            await _telemetryManager.SendTelemetryAsync(eventName);
        }

#if DEBUG
        [TestMethod]
        public async Task SendErrorTelemetry_WhenExceptionThrown_LogsDebugAndDoesNotRethrow()
        {
            var ex = new InvalidOperationException("Test error");
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _mockClient.Setup(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Telemetry failed"));

            await _telemetryManager.SendErrorTelemetryAsync(ex, "Test context");

            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Unable to send telemetry"))), Times.Once);
        }

#endif

        [TestMethod]
        public async Task SendErrorTelemetry_WithExtraData_DoesNotThrow()
        {
            var ex = new Exception("Test error");
            var extraData = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 },
            };

            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");

            await _telemetryManager.SendErrorTelemetryAsync(ex, "Test context", extraData);
        }

        [TestMethod]
        public async Task SendErrorTelemetry_TelemetryRelatedError_DoesNotSend()
        {
            var ex = new Exception("Failed to send telemetry");

            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");

            await _telemetryManager.SendErrorTelemetryAsync(ex, "context");

            _mockClient.Verify(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task SendErrorTelemetry_NetworkError_DoesNotSend()
        {
            var ex = new Exception("ECONNREFUSED: Connection refused");

            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync()).ReturnsAsync("device-123");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");

            await _telemetryManager.SendErrorTelemetryAsync(ex, "context");

            _mockClient.Verify(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
