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

        private static Func<CpuSnapshot> _snapshotProvider = DefaultSnapshotProvider;
        private static CpuSnapshot _previousSnapshot = DefaultSnapshotProvider();

        static CpuMonitor()
        {
        }

        public static void SetSnapshotProvider(Func<CpuSnapshot> provider)
        {
            _snapshotProvider = provider ?? DefaultSnapshotProvider;
            _previousSnapshot = _snapshotProvider();
        }

        public static void ResetSnapshotProvider()
        {
            _snapshotProvider = DefaultSnapshotProvider;
            _previousSnapshot = _snapshotProvider();
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

                var currentSnapshot = _snapshotProvider();

                if (_previousSnapshot != null)
                {
                    var cpuTimeDiff = currentSnapshot.TotalProcessorTime - _previousSnapshot.TotalProcessorTime;
                    var wallTimeDiff = currentSnapshot.Timestamp - _previousSnapshot.Timestamp;

                    if (wallTimeDiff.TotalMilliseconds > 0)
                    {
                        var usage = (cpuTimeDiff.TotalMilliseconds / wallTimeDiff.TotalMilliseconds) * 100.0 / coreCount;
                        usageSum += Math.Min(100, Math.Max(0, usage));
                    }
                }

                _previousSnapshot = currentSnapshot;
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
