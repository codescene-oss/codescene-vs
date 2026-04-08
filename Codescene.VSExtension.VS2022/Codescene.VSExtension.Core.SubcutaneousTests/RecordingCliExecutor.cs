// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public sealed class RecordingCliExecutor : ICliExecutor
{
    private readonly ICliExecutor _inner;
    private readonly EventJournal _journal;
    private readonly ConcurrentDictionary<string, ReviewBlockHandle> _deltaBlocks = new ConcurrentDictionary<string, ReviewBlockHandle>(StringComparer.OrdinalIgnoreCase);

    public RecordingCliExecutor(ICliExecutor inner, EventJournal journal)
    {
        _inner = inner;
        _journal = journal;
    }

    public ReviewBlockHandle BlockNextDelta(string path)
    {
        var handle = new ReviewBlockHandle(path);
        _deltaBlocks[path] = handle;
        return handle;
    }

    public async Task<DeltaResponseModel> ReviewDeltaAsync(ReviewDeltaRequest request, CancellationToken cancellationToken = default)
    {
        _journal.Record("cli.delta.started", request?.FilePath);

        if (request?.FilePath != null && _deltaBlocks.TryRemove(request.FilePath, out var block))
        {
            block.MarkEntered();
            await block.WaitForReleaseAsync(cancellationToken);
        }

        try
        {
            var result = await _inner.ReviewDeltaAsync(request, cancellationToken);
            _journal.Record("cli.delta.completed", request?.FilePath, $"result={result != null}");
            return result!;
        }
        catch (OperationCanceledException)
        {
            _journal.Record("cli.delta.canceled", request?.FilePath);
            throw;
        }
    }

    public async Task<CliReviewModel> ReviewContentAsync(string filename, string content, bool isBaseLine = false, CancellationToken cancellationToken = default)
    {
        _journal.Record("cli.review.started", filename, $"baseline={isBaseLine}");
        try
        {
            var result = await _inner.ReviewContentAsync(filename, content, isBaseLine, cancellationToken);
            _journal.Record("cli.review.completed", filename, $"result={result != null};baseline={isBaseLine}");
            return result!;
        }
        catch (OperationCanceledException)
        {
            _journal.Record("cli.review.canceled", filename, $"baseline={isBaseLine}");
            throw;
        }
    }

    public Task<string> GetFileVersionAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetFileVersionAsync(cancellationToken);
    }

    public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetDeviceIdAsync(cancellationToken);
    }

    public Task<PreFlightResponseModel> PreflightAsync(bool force = true, CancellationToken cancellationToken = default)
    {
        return _inner.PreflightAsync(force, cancellationToken);
    }

    public Task<RefactorResponseModel> PostRefactoringAsync(FnToRefactorModel fnToRefactor, bool skipCache = false, string? token = null, CancellationToken cancellationToken = default)
    {
        return _inner.PostRefactoringAsync(fnToRefactor, skipCache, token, cancellationToken);
    }

    public Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
    {
        return _inner.FnsToRefactorFromCodeSmellsAsync(fileName, fileContent, codeSmells, preflight, cancellationToken);
    }

    public Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResponse, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
    {
        return _inner.FnsToRefactorFromDeltaAsync(fileName, fileContent, deltaResponse, preflight, cancellationToken);
    }
}
