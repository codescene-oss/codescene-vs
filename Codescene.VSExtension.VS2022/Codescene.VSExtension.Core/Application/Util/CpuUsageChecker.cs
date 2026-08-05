// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Application.Util
{
    [Export(typeof(ICpuUsageChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CpuUsageChecker : ICpuUsageChecker
    {
        private const int Samples = 5;
        private const int SampleDelayMs = 13;

        private static readonly CpuThreshold[] CpuThresholds =
        {
            new CpuThreshold { MinCores = 8, Threshold = 85 },
            new CpuThreshold { MinCores = 4, Threshold = 80 },
            new CpuThreshold { MinCores = 0, Threshold = 75 },
        };

        private readonly ILogger _logger;
        private readonly Func<Task<double>> _sampleFn;
        private readonly Func<int> _coreCountProvider;

        [ImportingConstructor]
        public CpuUsageChecker(ILogger logger)
            : this(logger, null, null)
        {
        }

        internal CpuUsageChecker(
            ILogger logger,
            Func<Task<double>> sampleFn,
            Func<int> coreCountProvider)
        {
            _logger = logger;
            _sampleFn = sampleFn ?? DefaultSampleAsync;
            _coreCountProvider = coreCountProvider ?? (() => Environment.ProcessorCount);
        }

        public async Task<bool> IsCpuTooBusyAsync()
        {
            try
            {
                var coreCount = _coreCountProvider();
                double usageSum = 0;

                for (int i = 0; i < Samples; i++)
                {
                    if (i > 0)
                    {
                        await Task.Delay(SampleDelayMs);
                    }

                    usageSum += await _sampleFn();
                }

                var averageUsage = usageSum / Samples;
                var threshold = GetThresholdForCoreCount(coreCount);

                return averageUsage > threshold;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"CPU check failed: {ex.Message}");
                return false;
            }
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

            return 75;
        }

        private static async Task<double> DefaultSampleAsync()
        {
            return await Task.Run(() => CpuMonitor.TakeSampleSync());
        }

        private class CpuThreshold
        {
            public int MinCores { get; set; }

            public int Threshold { get; set; }
        }
    }
}
