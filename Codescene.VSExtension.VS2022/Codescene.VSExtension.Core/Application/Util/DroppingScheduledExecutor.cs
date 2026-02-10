// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Util
{
    /// <summary>
    /// Executes an async action on a periodic schedule, "dropping" (skipping) executions
    /// if the previous one is still running. Guarantees that no action executes after
    /// Stop() or Dispose() returns.
    /// </summary>
    public class DroppingScheduledExecutor : IDisposable
    {
        private readonly object _lock = new object();

        private readonly Func<Task> _action;
        private readonly Func<Task> _wrappedAction;
        private readonly TimeSpan _interval;
        private readonly ILogger _logger;

        // Signaled when no execution is in progress. Stop()/Dispose() wait on this
        // to guarantee no action runs after they return. Starts signaled (true).
        private readonly ManualResetEventSlim _notExecuting = new ManualResetEventSlim(true);

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

            // Secondary defense: early-exit if stopped. The primary gate is TryBeginExecution().
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
                StartImpl();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                StopImpl();
            }

            // Wait for any in-flight execution to complete before returning.
            // This guarantees no action runs after Stop() returns.
            _notExecuting.Wait();
        }

        public void Dispose()
        {
            // Track if already disposed to avoid double-wait and ObjectDisposedException.
            bool wasAlreadyDisposed;
            lock (_lock)
            {
                wasAlreadyDisposed = _disposed;
                DisposeImpl();
            }

            if (!wasAlreadyDisposed)
            {
                // Wait for any in-flight execution, then clean up the event.
                _notExecuting.Wait();
                _notExecuting.Dispose();
            }
        }

        private void StartImpl()
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

        private void StopImpl()
        {
            _stopped = true;
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            _logger.Debug("DroppingScheduledExecutor stopped");
        }

        private void DisposeImpl()
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
            _logger.Debug("DroppingScheduledExecutor disposed");
        }

        private bool TryBeginExecution()
        {
            lock (_lock)
            {
                return TryBeginExecutionImpl();
            }
        }

        // Atomic check-and-commit: either we fail the check, or we commit to execution
        // by setting _isRunning and resetting _notExecuting while still holding the lock.
        // This prevents the TOCTOU race where Stop/Dispose could slip in between check and execution.
        private bool TryBeginExecutionImpl()
        {
            if (_stopped || _disposed || _isRunning)
            {
                if (_isRunning)
                {
                    _logger.Debug("DroppingScheduledExecutor: dropping execution (previous still running)");
                }

                return false;
            }

            _isRunning = true;
            _notExecuting.Reset();
            return true;
        }

        private void EndExecution()
        {
            lock (_lock)
            {
                EndExecutionImpl();
            }

            // Signal outside lock to avoid potential deadlock with Stop()/Dispose() waiting.
            _notExecuting.Set();
        }

        private void EndExecutionImpl()
        {
            _isRunning = false;
        }

        private async void OnTimerCallback(object state)
        {
            if (!TryBeginExecution())
            {
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
                EndExecution();
            }
        }
    }
}
