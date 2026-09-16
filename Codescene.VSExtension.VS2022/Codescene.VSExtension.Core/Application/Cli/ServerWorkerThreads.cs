// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public static class ServerWorkerThreads
    {
        public static int Resolve(int configured)
        {
            if (configured > 0)
            {
                return configured;
            }

            return Math.Max(1, Environment.ProcessorCount / 2);
        }
    }
}
