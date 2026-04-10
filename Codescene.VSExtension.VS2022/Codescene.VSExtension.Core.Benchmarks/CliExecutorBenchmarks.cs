// Copyright (c) CodeScene. All rights reserved.

using BenchmarkDotNet.Attributes;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.FullCompressed]
public class CliExecutorBenchmarks
{
    private BenchmarkEnvironment _environment = null!;
    private CliExecutor _cliExecutor = null!;
    private ReviewDeltaRequest _deltaRequest = null!;
    private DeltaResponseModel _deltaResponse = null!;
    private PreFlightResponseModel _preflightResponse = null!;

    [GlobalSetup]
    public void Setup()
    {
        _environment = new BenchmarkEnvironment();
        _cliExecutor = _environment.CreateCliExecutor();

        var baselineReview = _cliExecutor.ReviewContentAsync(_environment.ExistingFilePath, BenchmarkInputs.BaselineCode).GetAwaiter().GetResult();
        var currentReview = _cliExecutor.ReviewContentAsync(_environment.ExistingFilePath, BenchmarkInputs.CurrentCode).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(baselineReview?.RawScore) || string.IsNullOrWhiteSpace(currentReview?.RawScore))
        {
            throw new InvalidOperationException("Unable to obtain review raw scores for delta benchmarks.");
        }

        _deltaRequest = new ReviewDeltaRequest
        {
            OldScore = baselineReview!.RawScore!,
            NewScore = currentReview!.RawScore!,
            FilePath = _environment.ExistingFilePath,
            FileContent = BenchmarkInputs.CurrentCode,
        };

        _deltaResponse = _cliExecutor.ReviewDeltaAsync(_deltaRequest).GetAwaiter().GetResult();
        if (_deltaResponse == null)
        {
            throw new InvalidOperationException("Unable to obtain a delta response for refactor benchmarks.");
        }

        _preflightResponse = _cliExecutor.PreflightAsync(force: true).GetAwaiter().GetResult();
        if (_preflightResponse == null)
        {
            throw new InvalidOperationException("Unable to obtain a preflight response for refactor benchmarks.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _environment?.Dispose();
    }

    [Benchmark]
    public Task<CliReviewModel> ReviewContentAsync()
    {
        return _cliExecutor.ReviewContentAsync(_environment.ExistingFilePath, BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<DeltaResponseModel> ReviewDeltaAsync()
    {
        return _cliExecutor.ReviewDeltaAsync(_deltaRequest);
    }

    [Benchmark]
    public Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync()
    {
        return _cliExecutor.FnsToRefactorFromDeltaAsync(
            BenchmarkInputs.FileName,
            BenchmarkInputs.CurrentCode,
            _deltaResponse,
            _preflightResponse);
    }
}
