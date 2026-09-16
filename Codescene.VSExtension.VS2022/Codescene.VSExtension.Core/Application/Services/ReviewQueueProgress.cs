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
        private bool _disposed;

        public ReviewQueueProgress(Action<ReviewQueue> onChange)
        {
            _onChange = onChange ?? throw new ArgumentNullException(nameof(onChange));
        }

        public void Update(ReviewQueue snapshot)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _latest = snapshot ?? new ReviewQueue();
                if (_latest.Count == 0)
                {
                    _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _onChange(_latest);
                    return;
                }

                if (_timer == null)
                {
                    _timer = new Timer(_ => Flush(), null, 0, Timeout.Infinite);
                }
                else
                {
                    _timer.Change(ThrottleMs, Timeout.Infinite);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
        }

        private void Flush()
        {
            ReviewQueue snapshot;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                snapshot = _latest;
            }

            _onChange(snapshot);
        }
    }
}
