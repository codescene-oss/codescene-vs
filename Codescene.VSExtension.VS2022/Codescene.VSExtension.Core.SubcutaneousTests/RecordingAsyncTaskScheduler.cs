// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public sealed class RecordingAsyncTaskScheduler : IAsyncTaskScheduler, IDisposable
{
    private readonly ConcurrentDictionary<int, Task> _pending = new ConcurrentDictionary<int, Task>();
    private readonly EventJournal _journal;
    private int _nextId;
    private bool _disposed;

    public RecordingAsyncTaskScheduler(EventJournal journal)
    {
        _journal = journal;
    }

    public void Schedule(Func<Task> asyncWork)
    {
        Schedule(_ => asyncWork(), CancellationToken.None);
    }

    public void Schedule(Func<CancellationToken, Task> asyncWork)
    {
        Schedule(asyncWork, CancellationToken.None);
    }

    public void Schedule(Func<CancellationToken, Task> asyncWork, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        var workId = Interlocked.Increment(ref _nextId);
        _journal.Record("scheduler.queued", detail: $"id={workId}");
        var task = Task.Run(
            async () =>
            {
                _journal.Record("scheduler.started", detail: $"id={workId}");
                try
                {
                    await asyncWork(cancellationToken);
                    _journal.Record("scheduler.completed", detail: $"id={workId}");
                }
                catch (OperationCanceledException)
                {
                    _journal.Record("scheduler.canceled", detail: $"id={workId}");
                }
                catch (Exception ex)
                {
                    _journal.Record("scheduler.failed", detail: $"id={workId};{ex.Message}");
                    throw;
                }
                finally
                {
                    _pending.TryRemove(workId, out _);
                }
            },
            CancellationToken.None);

        _pending[workId] = task;
    }

    public async Task WaitForIdleAsync(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (_pending.IsEmpty)
            {
                return;
            }

            await Task.Delay(50);
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
