// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Application.Util
{
    public class CpuSnapshot
    {
        public TimeSpan TotalProcessorTime { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
