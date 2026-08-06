// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Application.Util
{
    public class NoOpCpuUsageChecker : ICpuUsageChecker
    {
        public Task<bool> IsCpuTooBusyAsync()
        {
            return Task.FromResult(false);
        }
    }
}
