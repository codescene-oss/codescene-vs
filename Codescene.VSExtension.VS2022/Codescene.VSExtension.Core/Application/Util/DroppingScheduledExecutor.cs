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
        private readonly Func<Task> _wrappedAction;
        private readonly TimeSpan _interval;
        private readonly ILogger _logger;

        private Timer _timer;
        private bool _isRunning = false;
        private bool _stopped = false;
        private bool _disposed = false;

        public DroppingScheduledExecutor(Func<Task> action, TimeSpan interval, ILogger logger)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _action = action;
            _interval = interval;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _wrappedAction = async () =>
            {
                if (_stopped)
                {
                    return;
                }

                await _action();
            };
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(DroppingScheduledExecutor));
                }

                if (_timer != null)
                {
                    _logger.Warn("DroppingScheduledExecutor already started");
                    return;
                }

                _stopped = false;
                _timer = new Timer(OnTimerCallback, null, _interval, _interval);
            }

            _logger.Debug($"DroppingScheduledExecutor started with interval: {_interval.TotalSeconds}s");
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopped = true;
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
            }

            _logger.Debug("DroppingScheduledExecutor stopped");
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }

                _disposed = true;
            }

            _logger.Debug("DroppingScheduledExecutor disposed");
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
            }

            if (shouldExecute)
            {
                bool stillValid;
                lock (_lock)
                {
                    stillValid = !_stopped && !_disposed;
                }

                if (!stillValid)
                {
                    lock (_lock)
                    {
                        _isRunning = false;
                    }

                    return;
                }

                try
                {
                    _logger.Debug("DroppingScheduledExecutor: executing scheduled action");
                    await _wrappedAction();
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
            else
            {
                _logger.Debug("DroppingScheduledExecutor: dropping execution (previous still running)");
            }
        }
    }
}
