// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Util
{
    public class CpuSampler
    {
        internal const int DefaultSamples = 5;
        internal const int DefaultSampleDelayMs = 13;

        private static readonly CpuThreshold[] BaseThresholds =
        {
            new CpuThreshold { MinCores = 8, Threshold = 75 },
            new CpuThreshold { MinCores = 4, Threshold = 70 },
            new CpuThreshold { MinCores = 0, Threshold = 65 },
        };

        private readonly int _samples;
        private readonly int _sampleDelayMs;
        private readonly int _thresholdOffset;
        private readonly Func<CpuSnapshot> _snapshotProvider;
        private readonly Func<int> _coreCountProvider;
        private readonly Func<Task<double>> _usageProvider;

        private readonly object _snapshotLock = new object();
        private CpuSnapshot _previousSnapshot;

        public CpuSampler()
            : this(0, null, null)
        {
        }

        public CpuSampler(int thresholdOffset)
            : this(thresholdOffset, null, null)
        {
        }

        internal CpuSampler(
            int thresholdOffset,
            Func<CpuSnapshot> snapshotProvider,
            Func<int> coreCountProvider)
            : this(thresholdOffset, snapshotProvider, coreCountProvider, DefaultSamples, DefaultSampleDelayMs)
        {
        }

        internal CpuSampler(
            int thresholdOffset,
            Func<CpuSnapshot> snapshotProvider,
            Func<int> coreCountProvider,
            int samples,
            int sampleDelayMs)
        {
            _thresholdOffset = thresholdOffset;
            _samples = samples;
            _sampleDelayMs = sampleDelayMs;
            _coreCountProvider = coreCountProvider ?? (() => Environment.ProcessorCount);
            _snapshotProvider = snapshotProvider ?? DefaultSnapshotProvider;
            _previousSnapshot = _snapshotProvider();
            _usageProvider = null;
        }

        internal CpuSampler(
            int thresholdOffset,
            Func<Task<double>> usageProvider,
            Func<int> coreCountProvider,
            int samples,
            int sampleDelayMs)
        {
            _usageProvider = usageProvider;
            _snapshotProvider = null;
            _previousSnapshot = null;
            _thresholdOffset = thresholdOffset;
            _samples = samples;
            _sampleDelayMs = sampleDelayMs;
            _coreCountProvider = coreCountProvider ?? (() => Environment.ProcessorCount);
        }

        public static int GetThresholdForCoreCount(int coreCount, int thresholdOffset)
        {
            foreach (var t in BaseThresholds)
            {
                if (coreCount >= t.MinCores)
                {
                    return t.Threshold + thresholdOffset;
                }
            }

            return 65 + thresholdOffset;
        }

        public async Task<bool> IsCpuTooBusyAsync()
        {
            var coreCount = _coreCountProvider();

            double usageSum = 0;

            for (int i = 0; i < _samples; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(_sampleDelayMs);
                }

                if (_usageProvider != null)
                {
                    usageSum += await _usageProvider();
                }
                else
                {
                    usageSum += TakeSample(coreCount);
                }
            }

            var averageUsage = usageSum / _samples;
            var threshold = GetThresholdForCoreCount(coreCount);

            return averageUsage > threshold;
        }

        public double TakeSampleSync()
        {
            if (_snapshotProvider == null)
            {
                throw new InvalidOperationException(
                    "TakeSampleSync is not supported when CpuSampler is constructed with a usageProvider.");
            }

            var coreCount = _coreCountProvider();
            return TakeSample(coreCount);
        }

        public int GetThresholdForCoreCount(int coreCount)
        {
            return GetThresholdForCoreCount(coreCount, _thresholdOffset);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(
            out long idleTime,
            out long kernelTime,
            out long userTime);

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

        private double TakeSample(int coreCount)
        {
            lock (_snapshotLock)
            {
                try
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
                catch
                {
                    return 0;
                }
            }
        }

        private class CpuThreshold
        {
            public int MinCores { get; set; }

            public int Threshold { get; set; }
        }
    }
}
