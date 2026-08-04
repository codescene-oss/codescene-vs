// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Application.Util
{
    [Export(typeof(ICpuUsageThrottler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CpuUsageThrottler : ICpuUsageThrottler
    {
        private const int RetryDelayMs = 9000;

        private readonly ILogger _logger;
        private readonly Func<Task<bool>> _isCpuTooBusyFn;
        private readonly Func<int, CancellationToken, Task> _delayFn;

        [ImportingConstructor]
        public CpuUsageThrottler(ILogger logger)
            : this(logger, CpuMonitor.IsCpuTooBusyAsync, DefaultDelay)
        {
        }

        internal CpuUsageThrottler(
            ILogger logger,
            Func<Task<bool>> isCpuTooBusyFn,
            Func<int, CancellationToken, Task> delayFn)
        {
            _logger = logger;
            _isCpuTooBusyFn = isCpuTooBusyFn ?? CpuMonitor.IsCpuTooBusyAsync;
            _delayFn = delayFn ?? DefaultDelay;
        }

        public async Task WaitForCpuAsync(CancellationToken cancellationToken)
        {
            while (await _isCpuTooBusyFn())
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger?.Info($"CPU too busy, waiting {RetryDelayMs}ms before retry");
                await _delayFn(RetryDelayMs, cancellationToken);
            }
        }

        private static Task DefaultDelay(int ms, CancellationToken cancellationToken)
        {
            return Task.Delay(ms, cancellationToken);
        }
    }
}
