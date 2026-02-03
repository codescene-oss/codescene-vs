// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces.Util
{
    public interface IDebounceService
    {
        void Debounce(string key, Action action, TimeSpan delay);
    }
}
