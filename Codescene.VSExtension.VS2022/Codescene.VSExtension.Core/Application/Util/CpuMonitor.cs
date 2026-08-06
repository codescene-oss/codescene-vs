// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Util
{
    public static class CpuMonitor
    {
        private static readonly object _samplerLock = new object();
        private static CpuSampler _sampler = new CpuSampler();
        private static Func<CpuSnapshot> _customSnapshotProvider;
        private static Func<int> _customCoreCountProvider;

        static CpuMonitor()
        {
        }

        public static void SetSnapshotProvider(Func<CpuSnapshot> provider)
        {
            lock (_samplerLock)
            {
                _customSnapshotProvider = provider;
                RecreateSampler();
            }
        }

        public static void ResetSnapshotProvider()
        {
            lock (_samplerLock)
            {
                _customSnapshotProvider = null;
                RecreateSampler();
            }
        }

        public static void SetCoreCountProvider(Func<int> provider)
        {
            lock (_samplerLock)
            {
                _customCoreCountProvider = provider;
                RecreateSampler();
            }
        }

        public static void ResetCoreCountProvider()
        {
            lock (_samplerLock)
            {
                _customCoreCountProvider = null;
                RecreateSampler();
            }
        }

        public static async Task<bool> IsCpuTooBusyAsync()
        {
            CpuSampler sampler;
            lock (_samplerLock)
            {
                sampler = _sampler;
            }

            return await sampler.IsCpuTooBusyAsync();
        }

        internal static int GetThresholdForCoreCount(int coreCount)
        {
            CpuSampler sampler;
            lock (_samplerLock)
            {
                sampler = _sampler;
            }

            return sampler.GetThresholdForCoreCount(coreCount);
        }

        internal static double TakeSampleSync()
        {
            CpuSampler sampler;
            lock (_samplerLock)
            {
                sampler = _sampler;
            }

            return sampler.TakeSampleSync();
        }

        private static void RecreateSampler()
        {
            _sampler = new CpuSampler(0, _customSnapshotProvider, _customCoreCountProvider);
        }
    }
}
