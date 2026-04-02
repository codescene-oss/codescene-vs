// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public sealed class EventJournal
{
    private readonly ConcurrentQueue<EventRecord> _events = new ConcurrentQueue<EventRecord>();
    private int _nextSequence;

    public void Record(string name, string? path = null, string? detail = null)
    {
        _events.Enqueue(new EventRecord(
            Interlocked.Increment(ref _nextSequence),
            DateTimeOffset.UtcNow,
            name,
            path,
            detail));
    }

    public IReadOnlyList<EventRecord> Snapshot()
    {
        return _events.ToArray();
    }

    public int Count(string name, string? path = null)
    {
        return _events.Count(x =>
            x.Name == name &&
            (path == null || string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)));
    }

    public string Dump()
    {
        return string.Join(
            Environment.NewLine,
            Snapshot().Select(x => $"{x.Sequence:0000} {x.Timestamp:HH:mm:ss.fff} {x.Name} | {x.Path ?? "-"} | {x.Detail ?? "-"}"));
    }
}

public sealed class EventRecord
{
    public EventRecord(int sequence, DateTimeOffset timestamp, string name, string? path, string? detail)
    {
        Sequence = sequence;
        Timestamp = timestamp;
        Name = name;
        Path = path;
        Detail = detail;
    }

    public int Sequence { get; }

    public DateTimeOffset Timestamp { get; }

    public string Name { get; }

    public string? Path { get; }

    public string? Detail { get; }
}
