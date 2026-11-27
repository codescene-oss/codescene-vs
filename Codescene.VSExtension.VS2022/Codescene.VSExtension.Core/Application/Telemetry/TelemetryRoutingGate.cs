// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Codescene.VSExtension.Core.Application.Telemetry
{
    public sealed class TelemetryRoutingGate<T>
    {
        private readonly object _lock = new object();
        private readonly Action<T> _send;
        private readonly Func<T, bool> _isFirst;
        private readonly TimeSpan _bound;
        private readonly int _maxHeld;
        private readonly List<T> _queue = new List<T>();

        private int _generation;
        private bool _held;
        private bool _closed;
        private Timer _boundTimer;

        public TelemetryRoutingGate(Action<T> send, Func<T, bool> isFirst = null, TimeSpan? bound = null, int maxHeld = 100)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _isFirst = isFirst;
            _bound = bound ?? TimeSpan.FromSeconds(15);
            _maxHeld = maxHeld;
        }

        public bool IsHeld
        {
            get
            {
                lock (_lock)
                {
                    return _held;
                }
            }
        }

        public int Hold()
        {
            lock (_lock)
            {
                if (_closed)
                {
                    return _generation;
                }

                _generation++;

                if (!_held)
                {
                    _held = true;
                    StartBoundTimer();
                }

                return _generation;
            }
        }

        public void Ready(int generation)
        {
            ReleaseWhen(() => generation == _generation && _held);
        }

        public bool TryEnqueue(T item)
        {
            lock (_lock)
            {
                if (!_held)
                {
                    return false;
                }

                _queue.Add(item);
                if (_queue.Count > _maxHeld)
                {
                    _queue.RemoveAt(0);
                }

                return true;
            }
        }

        public void Close()
        {
            ReleaseWhen(() =>
            {
                if (_closed)
                {
                    return false;
                }

                _closed = true;
                return true;
            });
        }

        private void StartBoundTimer()
        {
            DisposeBoundTimer();
            _boundTimer = new Timer(_ => OnBoundElapsed(), null, _bound, Timeout.InfiniteTimeSpan);
        }

        private void DisposeBoundTimer()
        {
            _boundTimer?.Dispose();
            _boundTimer = null;
        }

        private void OnBoundElapsed()
        {
            ReleaseWhen(() => _held);
        }

        private void ReleaseWhen(Func<bool> shouldRelease)
        {
            List<T> toSend;
            lock (_lock)
            {
                if (!shouldRelease())
                {
                    return;
                }

                toSend = TakeQueue();
            }

            foreach (var item in toSend)
            {
                _send(item);
            }
        }

        private List<T> TakeQueue()
        {
            _held = false;
            DisposeBoundTimer();

            var items = new List<T>(_queue);
            _queue.Clear();

            if (_isFirst != null)
            {
                var firstIndex = items.FindIndex(item => _isFirst(item));
                if (firstIndex > 0)
                {
                    var first = items[firstIndex];
                    items.RemoveAt(firstIndex);
                    items.Insert(0, first);
                }
            }

            return items;
        }
    }
}
