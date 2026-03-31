// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Util
{
    public class PerFileRequestQueue<TRequest>
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, QueueState> _states = new Dictionary<string, QueueState>(StringComparer.OrdinalIgnoreCase);

        public bool TryStart(string fileKey, TRequest request)
        {
            if (string.IsNullOrWhiteSpace(fileKey))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(fileKey));
            }

            lock (_lock)
            {
                if (_states.TryGetValue(fileKey, out var state))
                {
                    if (!state.IsRunning)
                    {
                        state.IsRunning = true;
                        state.HasPending = false;
                    }

                    return false;
                }

                _states[fileKey] = new QueueState
                {
                    IsRunning = true,
                    PendingRequest = request,
                    HasPending = false,
                };
                return true;
            }
        }

        public void EnqueueLatest(string fileKey, TRequest request)
        {
            if (string.IsNullOrWhiteSpace(fileKey))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(fileKey));
            }

            lock (_lock)
            {
                if (!_states.TryGetValue(fileKey, out var state))
                {
                    _states[fileKey] = new QueueState
                    {
                        PendingRequest = request,
                        HasPending = true,
                    };
                    return;
                }

                state.PendingRequest = request;
                state.HasPending = true;
            }
        }

        public bool CompleteAndGetNext(string fileKey, out TRequest nextRequest)
        {
            if (string.IsNullOrWhiteSpace(fileKey))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(fileKey));
            }

            lock (_lock)
            {
                if (!_states.TryGetValue(fileKey, out var state))
                {
                    nextRequest = default;
                    return false;
                }

                if (!state.HasPending)
                {
                    _states.Remove(fileKey);
                    nextRequest = default;
                    return false;
                }

                nextRequest = state.PendingRequest;
                state.HasPending = false;
                state.PendingRequest = default;
                state.IsRunning = true;
                return true;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _states.Clear();
            }
        }

        private class QueueState
        {
            public bool IsRunning { get; set; }

            public bool HasPending { get; set; }

            public TRequest PendingRequest { get; set; }
        }
    }
}
