// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Application.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CpuMonitorTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            CpuMonitor.ResetSnapshotProvider();
            CpuMonitor.ResetCoreCountProvider();
        }

        [TestMethod]
        [DataRow(0, 65)]
        [DataRow(1, 65)]
        [DataRow(2, 65)]
        [DataRow(3, 65)]
        [DataRow(4, 70)]
        [DataRow(5, 70)]
        [DataRow(6, 70)]
        [DataRow(7, 70)]
        [DataRow(8, 75)]
        [DataRow(9, 75)]
        [DataRow(16, 75)]
        public void GetThresholdForCoreCount_ReturnsCorrectThreshold(int coreCount, int expectedThreshold)
        {
            var result = CpuMonitor.GetThresholdForCoreCount(coreCount);
            Assert.AreEqual(expectedThreshold, result);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenCpuBelowThreshold_ReturnsFalse()
        {
            var callCount = 0;
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    IdleTime = callCount * 900,
                    KernelTime = callCount * 1000,
                    UserTime = callCount * 100,
                };
            });

            var result = await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenCpuAboveThreshold_ReturnsTrue()
        {
            var callCount = 0;
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    IdleTime = callCount * 100,
                    KernelTime = callCount * 1000,
                    UserTime = callCount * 100,
                };
            });

            var result = await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_TakesMultipleSamples()
        {
            var callCount = 0;
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    IdleTime = callCount * 500,
                    KernelTime = callCount * 1000,
                    UserTime = callCount * 100,
                };
            });

            callCount = 0;
            await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsGreaterThanOrEqualTo(callCount, 5);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenTotalTimeDiffIsZeroOrNegative_ReturnsFalse()
        {
            CpuMonitor.SetSnapshotProvider(() =>
            {
                return new CpuSnapshot
                {
                    IdleTime = 0,
                    KernelTime = 0,
                    UserTime = 0,
                };
            });

            var result = await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TakeSampleSync_ReturnsExpectedCpuUsage()
        {
            var callCount = 0;
            CpuMonitor.SetCoreCountProvider(() => 8);
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    IdleTime = callCount * 500,
                    KernelTime = callCount * 1000,
                    UserTime = callCount * 100,
                };
            });

            var result = CpuMonitor.TakeSampleSync();

            Assert.IsTrue(result >= 0 && result <= 100);
        }

        [TestMethod]
        [DataRow(8, 20, false, DisplayName = "8 cores at 20% usage - below 75% threshold")]
        [DataRow(8, 75, false, DisplayName = "8 cores at 75% usage - at 75% threshold")]
        [DataRow(8, 80, true, DisplayName = "8 cores at 80% usage - above 75% threshold")]
        [DataRow(6, 20, false, DisplayName = "6 cores at 20% usage - below 70% threshold")]
        [DataRow(6, 70, false, DisplayName = "6 cores at 70% usage - at 70% threshold")]
        [DataRow(6, 75, true, DisplayName = "6 cores at 75% usage - above 70% threshold")]
        [DataRow(2, 20, false, DisplayName = "2 cores at 20% usage - below 65% threshold")]
        [DataRow(2, 65, false, DisplayName = "2 cores at 65% usage - at 65% threshold")]
        [DataRow(2, 70, true, DisplayName = "2 cores at 70% usage - above 65% threshold")]
        public async Task IsCpuTooBusyAsync_WithMockedCoreCount_VerifiesThresholdTiers(
            int coreCount, int usagePercent, bool expectedTooBusy)
        {
            var callCount = 0;

            CpuMonitor.SetCoreCountProvider(() => coreCount);

            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                var idlePercent = 100 - usagePercent;
                return new CpuSnapshot
                {
                    IdleTime = callCount * 1000 * idlePercent,
                    KernelTime = callCount * 1000 * 100,
                    UserTime = 0,
                };
            });

            var result = await CpuMonitor.IsCpuTooBusyAsync();

            Assert.AreEqual(expectedTooBusy, result);
        }
    }
}
