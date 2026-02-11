// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Util
{
    public class DroppingScheduledExecutor : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Func<Task> _action;
        private readonly TimeSpan _interval;
        private readonly ILogger _logger;

        private Timer _timer;
        private bool _isRunning;
        private bool _stopped;
        private bool _disposed;

        public DroppingScheduledExecutor(Func<Task> action, TimeSpan interval, ILogger logger)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _interval = interval;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                if (_timer != null)
                {
                    _logger.Warn("DroppingScheduledExecutor already started");
                    return;
                }

                _stopped = false;
                _timer = new Timer(OnTimerCallback, null, _interval, _interval);
                _logger.Debug($"DroppingScheduledExecutor started with interval: {_interval.TotalSeconds}s");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopped = true;
                _timer?.Dispose();
                _timer = null;
                _logger.Debug("DroppingScheduledExecutor stopped");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _timer?.Dispose();
                _timer = null;
                _disposed = true;
                _logger.Debug("DroppingScheduledExecutor disposed");
            }
        }

        private async void OnTimerCallback(object state)
        {
            bool shouldExecute;
            lock (_lock)
            {
                shouldExecute = !_stopped && !_disposed && !_isRunning;
                if (shouldExecute)
                {
                    _isRunning = true;
                }
                else if (_isRunning)
                {
                    _logger.Debug("DroppingScheduledExecutor: dropping execution (previous still running)");
                }
            }

            if (!shouldExecute)
            {
                return;
            }

            try
            {
                _logger.Debug("DroppingScheduledExecutor: executing scheduled action");
                await _action();
            }
            catch (Exception ex)
            {
                _logger.Error("DroppingScheduledExecutor: error executing scheduled action", ex);
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                }
            }
        }
    }
}
