using System;

namespace Codescene.VSExtension.Core.Application.Services.Util
{
    public interface IDebounceService
    {
        void Debounce(string key, Action action, TimeSpan delay);
    }
}
