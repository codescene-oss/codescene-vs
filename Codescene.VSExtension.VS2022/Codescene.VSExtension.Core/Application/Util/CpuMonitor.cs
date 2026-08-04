// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Runtime.InteropServices;
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
        private static Func<int> _coreCountProvider = () => Environment.ProcessorCount;

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

        public static void SetCoreCountProvider(Func<int> provider)
        {
            lock (_snapshotLock)
            {
                _coreCountProvider = provider ?? (() => Environment.ProcessorCount);
            }
        }

        public static void ResetCoreCountProvider()
        {
            lock (_snapshotLock)
            {
                _coreCountProvider = () => Environment.ProcessorCount;
            }
        }

        public static async Task<bool> IsCpuTooBusyAsync()
        {
            int coreCount;
            lock (_snapshotLock)
            {
                coreCount = _coreCountProvider();
            }

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

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(
            out long idleTime,
            out long kernelTime,
            out long userTime);

        private static double TakeSample(int coreCount)
        {
            lock (_snapshotLock)
            {
                var current = _snapshotProvider();
                var previous = _previousSnapshot;
                _previousSnapshot = current;

                if (previous == null)
                {
                    return 0;
                }

                var idleDiff = current.IdleTime - previous.IdleTime;
                var kernelDiff = current.KernelTime - previous.KernelTime;
                var userDiff = current.UserTime - previous.UserTime;

                var totalDiff = kernelDiff + userDiff;

                if (totalDiff <= 0)
                {
                    return 0;
                }

                var usage = 100.0 - ((100.0 * idleDiff) / totalDiff);
                return Math.Min(100, Math.Max(0, usage));
            }
        }

        private static CpuSnapshot DefaultSnapshotProvider()
        {
            if (!GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
            {
                return new CpuSnapshot { IdleTime = 0, KernelTime = 0, UserTime = 0 };
            }

            return new CpuSnapshot
            {
                IdleTime = idleTime,
                KernelTime = kernelTime,
                UserTime = userTime,
            };
        }

        private class CpuThreshold
        {
            public int MinCores { get; set; }

            public int Threshold { get; set; }
        }
    }
}
