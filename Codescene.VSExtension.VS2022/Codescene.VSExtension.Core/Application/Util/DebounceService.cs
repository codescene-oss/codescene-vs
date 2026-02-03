using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Util
{
    [Export(typeof(IDebounceService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DebounceService : IDebounceService, IDisposable
    {
        private readonly ILogger _logger;

        private readonly object _lock = new ();
        private readonly Dictionary<string, CancellationTokenSource> _timers = new Dictionary<string, CancellationTokenSource>();

        [ImportingConstructor]
        public DebounceService(ILogger logger)
        {
            _logger = logger;
        }

        public void Debounce(string key, Action action, TimeSpan delay)
        {
            try
            {
                lock (_lock)
                {
                    if (_timers.TryGetValue(key, out var existingCts))
                    {
                        existingCts.Cancel();
                        existingCts.Dispose();
                    }

                    var cts = new CancellationTokenSource();
                    _timers[key] = cts;

                    _ = Task.Delay((int)delay.TotalMilliseconds, cts.Token)
                        .ContinueWith(
                            t =>
                        {
                            if (!t.IsCanceled)
                            {
                                lock (_lock)
                                {
                                    if (_timers.TryGetValue(key, out var currentCts) && currentCts == cts)
                                        _timers.Remove(key);
                                }
                                _logger.Debug($"Performing debounced action... [{key}]");
                                action();
                            }
                            cts.Dispose();
                        }, TaskScheduler.Default);
                }
            }
            catch (Exception e)
            {
                _logger.Error("Unable to perform debounced action", e);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var cts in _timers.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                _timers.Clear();
            }
        }
    }
}
