// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public sealed class RecordingCodeReviewer : ICodeReviewer
{
    private readonly ICodeReviewer _inner;
    private readonly EventJournal _journal;
    private readonly ConcurrentDictionary<string, int> _reviewWithDeltaCalls = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _activeReviewWithDeltaCalls = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _maxParallelism = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ReviewBlockHandle> _blocks = new ConcurrentDictionary<string, ReviewBlockHandle>(StringComparer.OrdinalIgnoreCase);

    public RecordingCodeReviewer(ICodeReviewer inner, EventJournal journal)
    {
        _inner = inner;
        _journal = journal;
    }

    public ReviewBlockHandle BlockNextReview(string path)
    {
        var handle = new ReviewBlockHandle(path);
        _blocks[path] = handle;
        return handle;
    }

    public int GetReviewWithDeltaCallCount(string path)
    {
        return _reviewWithDeltaCalls.TryGetValue(path, out var count) ? count : 0;
    }

    public int GetMaxParallelism(string path)
    {
        return _maxParallelism.TryGetValue(path, out var count) ? count : 0;
    }

    public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
    {
        return _inner.ReviewAsync(path, content, isBaseline, operationGeneration, cancellationToken);
    }

    public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string? precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default)
    {
        return _inner.DeltaAsync(review, currentCode, precomputedBaselineRawScore, operationGeneration, cancellationToken);
    }

    public Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
    {
        return _inner.ReviewAndBaselineAsync(path, currentCode, operationGeneration, cancellationToken);
    }

    public async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default)
    {
        _reviewWithDeltaCalls.AddOrUpdate(path, 1, (_, count) => count + 1);
        var activeCount = _activeReviewWithDeltaCalls.AddOrUpdate(path, 1, (_, count) => count + 1);
        _maxParallelism.AddOrUpdate(path, activeCount, (_, currentMax) => Math.Max(currentMax, activeCount));
        _journal.Record("review.started", path, $"active={activeCount}");

        if (_blocks.TryRemove(path, out var block))
        {
            block.MarkEntered();
            await block.WaitForReleaseAsync(cancellationToken);
        }

        try
        {
            var result = await _inner.ReviewWithDeltaAsync(path, content, operationGeneration, cancellationToken);
            _journal.Record("review.completed", path, $"delta={result.delta != null}");
            return result;
        }
        catch (OperationCanceledException)
        {
            _journal.Record("review.canceled", path);
            throw;
        }
        finally
        {
            _activeReviewWithDeltaCalls.AddOrUpdate(path, 0, (_, count) => Math.Max(0, count - 1));
        }
    }

    public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default)
    {
        return _inner.GetOrComputeBaselineRawScoreAsync(path, baselineContent, operationGeneration, cancellationToken);
    }
}

public sealed class ReviewBlockHandle
{
    private readonly TaskCompletionSource<bool> _entered = new TaskCompletionSource<bool>();
    private readonly TaskCompletionSource<bool> _released = new TaskCompletionSource<bool>();

    public ReviewBlockHandle(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public Task Entered => _entered.Task;

    public void MarkEntered()
    {
        _entered.TrySetResult(true);
    }

    public void Release()
    {
        _released.TrySetResult(true);
    }

    public async Task WaitForReleaseAsync(CancellationToken cancellationToken)
    {
        using (cancellationToken.Register(() => _released.TrySetCanceled()))
        {
            await _released.Task;
        }
    }
}
