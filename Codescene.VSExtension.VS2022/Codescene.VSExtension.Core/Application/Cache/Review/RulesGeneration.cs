// Copyright (c) CodeScene. All rights reserved.

using System.Threading;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public static class RulesGeneration
    {
        private static long _current;

        public static long Current => Interlocked.Read(ref _current);

        public static void Increment()
        {
            Interlocked.Increment(ref _current);
        }

        public static void Reset()
        {
            Interlocked.Exchange(ref _current, 0);
        }
    }
}
