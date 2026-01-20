namespace Codescene.VSExtension.Core.IntegrationTests.CliExecutor
{
    [TestClass]
    public class DeviceIdTests: BaseCliExecutorTests
    {
        [TestInitialize]
        public override void Initialize() => base.Initialize();

        [TestCleanup]
        public override void Cleanup() => base.Cleanup();

        [TestMethod]
        public void GetDeviceId_ReturnsNonEmptyStableId()
        {
            // Act
            var deviceId1 = CliExecutor.GetDeviceId();
            var deviceId2 = CliExecutor.GetDeviceId();

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(deviceId1), "Device ID should not be empty");
            Assert.AreEqual(deviceId1, deviceId2, "Device ID should be stable across calls");
        }
    }
}
