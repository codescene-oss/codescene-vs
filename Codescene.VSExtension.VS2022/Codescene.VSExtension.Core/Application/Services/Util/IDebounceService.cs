using System;

namespace Codescene.VSExtension.Core.Application.Services.Util
{
    public interface IDebounceService
    {
        void Debounce<T>(T arg, Action<T> action, TimeSpan delay);
    }
}
