// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CpuUsageCheckerTests
    {
        private Mock<Codescene.VSExtension.Core.Interfaces.ILogger> _mockLogger;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<Codescene.VSExtension.Core.Interfaces.ILogger>();
        }

        [TestMethod]
        [DataRow(0, 75)]
        [DataRow(1, 75)]
        [DataRow(2, 75)]
        [DataRow(3, 75)]
        [DataRow(4, 80)]
        [DataRow(5, 80)]
        [DataRow(6, 80)]
        [DataRow(7, 80)]
        [DataRow(8, 85)]
        [DataRow(9, 85)]
        [DataRow(16, 85)]
        public void GetThresholdForCoreCount_ReturnsCorrectThreshold_WithTenPercentHigherThanCpuMonitor(int coreCount, int expectedThreshold)
        {
            var result = CpuUsageChecker.GetThresholdForCoreCount(coreCount);
            Assert.AreEqual(expectedThreshold, result);
        }

        [TestMethod]
        public void GetThresholdForCoreCount_Is10PercentHigherThanCpuMonitor()
        {
            Assert.AreEqual(CpuMonitor.GetThresholdForCoreCount(2) + 10, CpuUsageChecker.GetThresholdForCoreCount(2));
            Assert.AreEqual(CpuMonitor.GetThresholdForCoreCount(6) + 10, CpuUsageChecker.GetThresholdForCoreCount(6));
            Assert.AreEqual(CpuMonitor.GetThresholdForCoreCount(8) + 10, CpuUsageChecker.GetThresholdForCoreCount(8));
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenBelowThreshold_ReturnsFalse()
        {
            var sampleCallCount = 0;
            var checker = new CpuUsageChecker(
                _mockLogger.Object,
                () =>
                {
                    sampleCallCount++;
                    return Task.FromResult(50.0);
                },
                () => 8);

            var result = await checker.IsCpuTooBusyAsync();

            Assert.IsFalse(result);
            Assert.AreEqual(5, sampleCallCount);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenAboveThreshold_ReturnsTrue()
        {
            var checker = new CpuUsageChecker(
                _mockLogger.Object,
                () => Task.FromResult(90.0),
                () => 8);

            var result = await checker.IsCpuTooBusyAsync();

            Assert.IsTrue(result);
        }

        [TestMethod]
        [DataRow(8, 50, false, DisplayName = "8 cores at 50% usage - below 85% threshold")]
        [DataRow(8, 85, false, DisplayName = "8 cores at 85% usage - at 85% threshold")]
        [DataRow(8, 90, true, DisplayName = "8 cores at 90% usage - above 85% threshold")]
        [DataRow(6, 50, false, DisplayName = "6 cores at 50% usage - below 80% threshold")]
        [DataRow(6, 80, false, DisplayName = "6 cores at 80% usage - at 80% threshold")]
        [DataRow(6, 85, true, DisplayName = "6 cores at 85% usage - above 80% threshold")]
        [DataRow(2, 50, false, DisplayName = "2 cores at 50% usage - below 75% threshold")]
        [DataRow(2, 75, false, DisplayName = "2 cores at 75% usage - at 75% threshold")]
        [DataRow(2, 80, true, DisplayName = "2 cores at 80% usage - above 75% threshold")]
        public async Task IsCpuTooBusyAsync_WithMockedCoreCount_VerifiesHigherThresholdTiers(
            int coreCount, int usagePercent, bool expectedTooBusy)
        {
            var checker = new CpuUsageChecker(
                _mockLogger.Object,
                () => Task.FromResult((double)usagePercent),
                () => coreCount);

            var result = await checker.IsCpuTooBusyAsync();

            Assert.AreEqual(expectedTooBusy, result);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenExceptionOccurs_ReturnsFalseAndLogs()
        {
            var checker = new CpuUsageChecker(
                _mockLogger.Object,
                () => throw new System.InvalidOperationException("Test exception"),
                () => 8);

            var result = await checker.IsCpuTooBusyAsync();

            Assert.IsFalse(result);
            _mockLogger.Verify(x => x.Warn(It.Is<string>(s => s.Contains("CPU check failed"))), Times.Once);
        }

        [TestMethod]
        public async Task NoOpCpuUsageChecker_AlwaysReturnsFalse()
        {
            var checker = new NoOpCpuUsageChecker();

            var result = await checker.IsCpuTooBusyAsync();

            Assert.IsFalse(result);
        }
    }
}
