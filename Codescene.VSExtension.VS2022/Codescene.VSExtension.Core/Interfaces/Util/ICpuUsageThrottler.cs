// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Util
{
    public interface ICpuUsageThrottler
    {
        Task WaitForCpuAsync(CancellationToken cancellationToken);
    }
}
