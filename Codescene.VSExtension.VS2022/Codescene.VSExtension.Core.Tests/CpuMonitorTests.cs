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
            var baseTime = DateTime.UtcNow;
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    TotalProcessorTime = TimeSpan.FromMilliseconds(callCount * 10),
                    Timestamp = baseTime.AddMilliseconds(callCount * 100),
                };
            });

            var result = await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenCpuAboveThreshold_ReturnsTrue()
        {
            var callCount = 0;
            var baseTime = DateTime.UtcNow;
            var coreCount = Environment.ProcessorCount;
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    TotalProcessorTime = TimeSpan.FromMilliseconds(callCount * 100 * coreCount),
                    Timestamp = baseTime.AddMilliseconds(callCount * 100),
                };
            });

            var result = await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_TakesMultipleSamples()
        {
            var callCount = 0;
            var baseTime = DateTime.UtcNow;
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    TotalProcessorTime = TimeSpan.FromMilliseconds(callCount * 5),
                    Timestamp = baseTime.AddMilliseconds(callCount * 50),
                };
            });

            callCount = 0;
            await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsGreaterThanOrEqualTo(callCount, 5);
        }

        [TestMethod]
        public async Task IsCpuTooBusyAsync_WhenWallTimeDiffIsZeroOrNegative_ReturnsFalse()
        {
            var callCount = 0;
            var baseTime = DateTime.UtcNow;
            CpuMonitor.SetSnapshotProvider(() =>
            {
                callCount++;
                return new CpuSnapshot
                {
                    TotalProcessorTime = TimeSpan.FromMilliseconds(callCount * 1000),
                    Timestamp = baseTime,
                };
            });

            var result = await CpuMonitor.IsCpuTooBusyAsync();

            Assert.IsFalse(result);
        }
    }
}
