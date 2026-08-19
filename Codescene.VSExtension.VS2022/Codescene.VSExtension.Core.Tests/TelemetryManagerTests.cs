// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Telemetry;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Codescene.VSExtension.Core.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TelemetryManagerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<IIdeServerHost> _mockHost;
        private Mock<IIdeServerClient> _mockClient;
        private Mock<IDeviceIdStore> _mockDeviceIdStore;
        private Mock<IExtensionMetadataProvider> _mockMetadataProvider;
        private TelemetryManager _telemetryManager;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockClient = new Mock<IIdeServerClient>();
            _mockHost = new Mock<IIdeServerHost>();
            _mockHost.Setup(x => x.Client).Returns(_mockClient.Object);
            _mockDeviceIdStore = new Mock<IDeviceIdStore>();
            _mockMetadataProvider = new Mock<IExtensionMetadataProvider>();
            _mockMetadataProvider.Setup(x => x.GetEditorVersion()).Returns("18.1.1");
            _mockMetadataProvider.Setup(x => x.GetVersion()).Returns("1.0.0");
            _telemetryManager = new TelemetryManager(
                _mockLogger.Object,
                _mockHost.Object,
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

        [TestMethod]
        public async Task SendTelemetry_WhenExceptionThrown_LogsDebugAndDoesNotRethrow()
        {
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("device-123");
            _mockClient.Setup(x => x.TelemetryAsync(It.IsAny<TelemetryEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Telemetry failed"));

            await _telemetryManager.SendTelemetryAsync("test-event");

            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Unable to send telemetry"))), Times.Once);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_CallsTelemetryRpc()
        {
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("device-123");
            _mockClient.Setup(x => x.TelemetryAsync(It.IsAny<TelemetryEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TelemetryResponse { Status = 202 });

            await _telemetryManager.SendTelemetryAsync("test-event");

            _mockClient.Verify(x => x.TelemetryAsync(It.Is<TelemetryEvent>(e => e.EventName.Contains("test-event")), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_WhenServerNotRunning_DoesNotThrow()
        {
            _mockHost.Setup(x => x.Client).Returns((IIdeServerClient)null);
            _mockDeviceIdStore.Setup(x => x.GetDeviceIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("device-123");

            await _telemetryManager.SendTelemetryAsync("test-event");

            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("not running"))), Times.Once);
        }

        [TestMethod]
        public async Task SendErrorTelemetry_TelemetryRelatedError_DoesNotSend()
        {
            var ex = new Exception("Failed to send telemetry");

            await _telemetryManager.SendErrorTelemetryAsync(ex, "context");

            _mockClient.Verify(x => x.TelemetryAsync(It.IsAny<TelemetryEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
