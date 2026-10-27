// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Application.Util
{
    [Export(typeof(IDebounceService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DebounceService : IDebounceService, IDisposable
    {
        private readonly ILogger _logger;

        private readonly object _lock = new object();
        private readonly Dictionary<string, CancellationTokenSource> _timers = new Dictionary<string, CancellationTokenSource>();
        private readonly Dictionary<string, ThrottleWindow> _throttles = new Dictionary<string, ThrottleWindow>();

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
                                    {
                                        _timers.Remove(key);
                                    }
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

        public void Throttle(string key, Action action, TimeSpan interval)
        {
            Action leading = null;
            try
            {
                lock (_lock)
                {
                    leading = BeginThrottle(key, action, interval);
                }

                leading?.Invoke();
            }
            catch (Exception e)
            {
                _logger.Error("Unable to perform throttled action", e);
            }
        }

        public void Cancel(string key)
        {
            lock (_lock)
            {
                CancelDebounce(key);
                CancelThrottle(key);
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
                foreach (var window in _throttles.Values)
                {
                    window.Timer?.Dispose();
                }

                _throttles.Clear();
            }
        }

        private Action BeginThrottle(string key, Action action, TimeSpan interval)
        {
            if (!_throttles.TryGetValue(key, out var window))
            {
                window = new ThrottleWindow();
                _throttles[key] = window;
            }

            if (window.Open)
            {
                window.Trailing = action;
                return null;
            }

            window.Open = true;
            window.Trailing = null;
            window.Timer?.Dispose();
            window.Timer = new Timer(_ => FireThrottle(key), null, (int)interval.TotalMilliseconds, Timeout.Infinite);
            return action;
        }

        private void FireThrottle(string key)
        {
            Action trailing = null;
            lock (_lock)
            {
                if (!_throttles.TryGetValue(key, out var window))
                {
                    return;
                }

                window.Open = false;
                trailing = window.Trailing;
                window.Trailing = null;
            }

            if (trailing == null)
            {
                return;
            }

            try
            {
                _logger.Debug($"Performing throttled action... [{key}]");
                trailing();
            }
            catch (Exception e)
            {
                _logger.Error("Unable to perform throttled action", e);
            }
        }

        private void CancelDebounce(string key)
        {
            if (!_timers.TryGetValue(key, out var existingCts))
            {
                return;
            }

            existingCts.Cancel();
            existingCts.Dispose();
            _timers.Remove(key);
        }

        private void CancelThrottle(string key)
        {
            if (!_throttles.TryGetValue(key, out var window))
            {
                return;
            }

            window.Open = false;
            window.Trailing = null;
            window.Timer?.Dispose();
            window.Timer = null;
            _throttles.Remove(key);
        }

        private sealed class ThrottleWindow
        {
            public Timer Timer { get; set; }

            public Action Trailing { get; set; }

            public bool Open { get; set; }
        }
    }
}
