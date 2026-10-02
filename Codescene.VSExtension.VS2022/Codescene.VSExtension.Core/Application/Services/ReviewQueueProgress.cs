// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Application.Services
{
    public sealed class ReviewQueueProgress : IDisposable
    {
        public const int ThrottleMs = 250;
        private readonly Action<ReviewQueue> _onChange;
        private readonly object _gate = new object();
        private ReviewQueue _latest = new ReviewQueue();
        private Timer _timer;
        private bool _windowOpen;
        private bool _trailing;
        private bool _disposed;

        public ReviewQueueProgress(Action<ReviewQueue> onChange)
        {
            _onChange = onChange ?? throw new ArgumentNullException(nameof(onChange));
        }

        public void Update(ReviewQueue snapshot)
        {
            Action emit = null;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _latest = snapshot ?? new ReviewQueue();
                if (_latest.Count == 0)
                {
                    CloseWindow();
                    var cleared = _latest;
                    emit = () => _onChange(cleared);
                }
                else if (!_windowOpen)
                {
                    _windowOpen = true;
                    _trailing = false;
                    var leading = _latest;
                    Arm(ThrottleMs);
                    emit = () => _onChange(leading);
                }
                else
                {
                    _trailing = true;
                }
            }

            emit?.Invoke();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _windowOpen = false;
                _trailing = false;
                _timer?.Dispose();
                _timer = null;
            }
        }

        private void Flush()
        {
            ReviewQueue snapshot = null;
            lock (_gate)
            {
                if (_disposed || !_trailing)
                {
                    _windowOpen = false;
                    return;
                }

                _trailing = false;
                _windowOpen = false;
                snapshot = _latest;
            }

            if (snapshot != null)
            {
                _onChange(snapshot);
            }
        }

        private void CloseWindow()
        {
            _windowOpen = false;
            _trailing = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void Arm(int dueMs)
        {
            if (_timer == null)
            {
                _timer = new Timer(_ => Flush(), null, dueMs, Timeout.Infinite);
                return;
            }

            _timer.Change(dueMs, Timeout.Infinite);
        }
    }
}
