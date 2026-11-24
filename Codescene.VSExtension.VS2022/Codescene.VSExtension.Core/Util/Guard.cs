// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Util
{
    internal static class Guard
    {
        public static T NotNull<T>(T value, string name)
            where T : class
        {
            return value ?? throw new ArgumentNullException(name);
        }
    }
}
