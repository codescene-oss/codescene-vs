// Copyright (c) CodeScene. All rights reserved.

using BenchmarkDotNet.Attributes;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.FullCompressed]
public class CachingCodeReviewerBenchmarks
{
    private BenchmarkEnvironment _environment = null!;
    private FixedGitService _gitService = null!;
    private FixedPreflightManager _preflightManager = null!;
    private CachingCodeReviewer _coldReviewReviewer = null!;
    private CachingCodeReviewer _warmReviewReviewer = null!;
    private CachingCodeReviewer _warmDeltaReviewer = null!;
    private string _warmReviewPath = null!;
    private int _pathCounter;

    [GlobalSetup]
    public void Setup()
    {
        _environment = new BenchmarkEnvironment();
        _gitService = new FixedGitService(BenchmarkInputs.BaselineCode);

        var preflightResponse = _environment.CreateCliExecutor().PreflightAsync(force: true).GetAwaiter().GetResult();
        if (preflightResponse == null)
        {
            throw new InvalidOperationException("Unable to obtain a preflight response for caching benchmarks.");
        }

        _preflightManager = new FixedPreflightManager(preflightResponse);
        _coldReviewReviewer = _environment.CreateCachingCodeReviewer(_gitService);

        _warmReviewReviewer = _environment.CreateCachingCodeReviewer(_gitService);
        _warmReviewPath = Path.Combine(_environment.RootDirectory, "cached-review.cs");
        _warmReviewReviewer.ReviewAsync(_warmReviewPath, BenchmarkInputs.CurrentCode).GetAwaiter().GetResult();

        _warmDeltaReviewer = _environment.CreateCachingCodeReviewer(_gitService, _preflightManager);
        _warmDeltaReviewer.ReviewWithDeltaAsync(_environment.ExistingFilePath, BenchmarkInputs.CurrentCode).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _environment?.Dispose();
    }

    [Benchmark]
    public Task<FileReviewModel> ReviewAsyncCold()
    {
        return _coldReviewReviewer.ReviewAsync(NextPath("cached-review-miss"), BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<FileReviewModel> ReviewAsyncWarm()
    {
        return _warmReviewReviewer.ReviewAsync(_warmReviewPath, BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsyncCold()
    {
        return _environment.CreateCachingCodeReviewer(_gitService, _preflightManager)
            .ReviewWithDeltaAsync(_environment.ExistingFilePath, BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsyncWarm()
    {
        return _warmDeltaReviewer.ReviewWithDeltaAsync(_environment.ExistingFilePath, BenchmarkInputs.CurrentCode);
    }

    private string NextPath(string prefix)
    {
        var index = Interlocked.Increment(ref _pathCounter);
        return Path.Combine(_environment.RootDirectory, prefix + "-" + index + ".cs");
    }
}
