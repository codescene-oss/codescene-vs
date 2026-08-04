// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Util
{
    public static class CpuMonitor
    {
        private const int Samples = 5;
        private const int SampleDelayMs = 13;

        private static readonly CpuThreshold[] CpuThresholds =
        {
            new CpuThreshold { MinCores = 8, Threshold = 75 },
            new CpuThreshold { MinCores = 4, Threshold = 70 },
            new CpuThreshold { MinCores = 0, Threshold = 65 },
        };

        private static readonly object _snapshotLock = new object();
        private static Func<CpuSnapshot> _snapshotProvider = DefaultSnapshotProvider;
        private static CpuSnapshot _previousSnapshot = DefaultSnapshotProvider();

        static CpuMonitor()
        {
        }

        public static void SetSnapshotProvider(Func<CpuSnapshot> provider)
        {
            lock (_snapshotLock)
            {
                _snapshotProvider = provider ?? DefaultSnapshotProvider;
                _previousSnapshot = _snapshotProvider();
            }
        }

        public static void ResetSnapshotProvider()
        {
            lock (_snapshotLock)
            {
                _snapshotProvider = DefaultSnapshotProvider;
                _previousSnapshot = _snapshotProvider();
            }
        }

        public static async Task<bool> IsCpuTooBusyAsync()
        {
            var coreCount = Environment.ProcessorCount;
            double usageSum = 0;

            for (int i = 0; i < Samples; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(SampleDelayMs);
                }

                usageSum += TakeSample(coreCount);
            }

            var averageUsage = usageSum / Samples;
            var threshold = GetThresholdForCoreCount(coreCount);

            return averageUsage > threshold;
        }

        internal static int GetThresholdForCoreCount(int coreCount)
        {
            foreach (var t in CpuThresholds)
            {
                if (coreCount >= t.MinCores)
                {
                    return t.Threshold;
                }
            }

            return 65;
        }

        private static double TakeSample(int coreCount)
        {
            lock (_snapshotLock)
            {
                var currentSnapshot = _snapshotProvider();
                var previous = _previousSnapshot;
                _previousSnapshot = currentSnapshot;

                if (previous == null)
                {
                    return 0;
                }

                var cpuTimeDiff = currentSnapshot.TotalProcessorTime - previous.TotalProcessorTime;
                var wallTimeDiff = currentSnapshot.Timestamp - previous.Timestamp;

                if (wallTimeDiff.TotalMilliseconds <= 0)
                {
                    return 0;
                }

                var usage = (cpuTimeDiff.TotalMilliseconds / wallTimeDiff.TotalMilliseconds) * 100.0 / coreCount;
                return Math.Min(100, Math.Max(0, usage));
            }
        }

        private static CpuSnapshot DefaultSnapshotProvider()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return new CpuSnapshot
                {
                    TotalProcessorTime = process.TotalProcessorTime,
                    Timestamp = DateTime.UtcNow,
                };
            }
        }

        private class CpuThreshold
        {
            public int MinCores { get; set; }

            public int Threshold { get; set; }
        }
    }
}
