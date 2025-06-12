using Codescene.VSExtension.Core.Application.Services.Util;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.Util;

[Export(typeof(IDebounceService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class DebounceService : IDebounceService
{
    private readonly ConcurrentDictionary<object, CancellationTokenSource> _debounceTokens = new();

    public void Debounce<T>(T arg, Action<T> action, TimeSpan delay)
    {
        var key = (object)arg ?? typeof(T);

        if (_debounceTokens.TryGetValue(key, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _debounceTokens[key] = cts;

        _ = Task.Delay(delay, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                action(arg);
                _debounceTokens.TryRemove(key, out _);
                cts.Dispose();
            }
        }, TaskScheduler.Default);
    }
}

