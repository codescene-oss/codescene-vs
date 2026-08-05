// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Application.Util
{
    public class CpuSnapshot
    {
        public long IdleTime { get; set; }

        public long KernelTime { get; set; }

        public long UserTime { get; set; }
    }
}
