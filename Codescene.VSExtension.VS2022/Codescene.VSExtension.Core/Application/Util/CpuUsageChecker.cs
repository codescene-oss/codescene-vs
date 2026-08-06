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
        private const int ThresholdOffset = 10;
        private const int Samples = 5;
        private const int SampleDelayMs = 13;

        private readonly ILogger _logger;
        private readonly CpuSampler _sampler;

        [ImportingConstructor]
        public CpuUsageChecker(ILogger logger)
            : this(logger, new CpuSampler(ThresholdOffset))
        {
        }

        internal CpuUsageChecker(
            ILogger logger,
            Func<Task<double>> sampleFn,
            Func<int> coreCountProvider)
            : this(logger, new CpuSampler(ThresholdOffset, sampleFn, coreCountProvider, Samples, SampleDelayMs))
        {
        }

        internal CpuUsageChecker(ILogger logger, CpuSampler sampler)
        {
            _logger = logger;
            _sampler = sampler;
        }

        public async Task<bool> IsCpuTooBusyAsync()
        {
            try
            {
                return await _sampler.IsCpuTooBusyAsync();
            }
            catch (Exception ex)
            {
                _logger?.Warn($"CPU check failed: {ex.Message}");
                return false;
            }
        }

        internal static int GetThresholdForCoreCount(int coreCount)
        {
            var sampler = new CpuSampler(ThresholdOffset);
            return sampler.GetThresholdForCoreCount(coreCount);
        }
    }
}
