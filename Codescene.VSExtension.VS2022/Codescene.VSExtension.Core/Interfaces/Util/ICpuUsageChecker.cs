// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Util
{
    public interface ICpuUsageChecker
    {
        Task<bool> IsCpuTooBusyAsync();
    }
}
