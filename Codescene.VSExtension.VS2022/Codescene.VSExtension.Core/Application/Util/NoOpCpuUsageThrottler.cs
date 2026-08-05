// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Application.Util
{
    public class NoOpCpuUsageThrottler : ICpuUsageThrottler
    {
        public Task WaitForCpuAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
