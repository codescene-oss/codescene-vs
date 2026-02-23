// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Util
{
    public static class CoreCountUtils
    {
        public static int GetParallelizationCountByCoreCount(int numberOfCores)
        {
            var calculatedCoreCount = (int)((long)numberOfCores * 33 / 100);
            return Math.Max(1, calculatedCoreCount);
        }
    }
}
