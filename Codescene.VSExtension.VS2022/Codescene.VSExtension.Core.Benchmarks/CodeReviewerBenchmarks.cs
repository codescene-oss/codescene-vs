// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.FullCompressed]
public class CodeReviewerBenchmarks
{
    private BenchmarkEnvironment _environment = null!;
    private FixedGitService _gitService = null!;
    private CodeReviewer _codeReviewer = null!;
    private CodeReviewer _codeReviewerWithPreflight = null!;
    private string _currentRawScore = null!;
    private int _pathCounter;

    [GlobalSetup]
    public void Setup()
    {
        new BaselineReviewCacheService().Clear();

        _environment = new BenchmarkEnvironment();
        _gitService = new FixedGitService(BenchmarkInputs.BaselineCode);
        _codeReviewer = _environment.CreateCodeReviewer(_gitService);

        var preflightResponse = _environment.CreateCliExecutor().PreflightAsync(force: true).GetAwaiter().GetResult();
        if (preflightResponse == null)
        {
            throw new InvalidOperationException("Unable to obtain a preflight response for CodeReviewer benchmarks.");
        }

        _codeReviewerWithPreflight = _environment.CreateCodeReviewer(_gitService, new FixedPreflightManager(preflightResponse));

        var currentReview = _environment.CreateCliExecutor().ReviewContentAsync(BenchmarkInputs.FileName, BenchmarkInputs.CurrentCode).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(currentReview?.RawScore))
        {
            throw new InvalidOperationException("Unable to obtain a review raw score for CodeReviewer benchmarks.");
        }

        _currentRawScore = currentReview!.RawScore!;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        new BaselineReviewCacheService().Clear();
        _environment?.Dispose();
    }

    [Benchmark]
    public Task<FileReviewModel> ReviewAsync()
    {
        return _codeReviewer.ReviewAsync(NextPath("review"), BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<string> GetOrComputeBaselineRawScoreAsync()
    {
        return _codeReviewer.GetOrComputeBaselineRawScoreAsync(NextPath("baseline"), BenchmarkInputs.BaselineCode);
    }

    [Benchmark]
    public Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync()
    {
        return _codeReviewer.ReviewAndBaselineAsync(NextPath("review-baseline"), BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<Codescene.VSExtension.Core.Models.Cli.Delta.DeltaResponseModel> DeltaAsync()
    {
        return _codeReviewer.DeltaAsync(
            new FileReviewModel { FilePath = NextPath("delta"), RawScore = _currentRawScore },
            BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<(FileReviewModel review, Codescene.VSExtension.Core.Models.Cli.Delta.DeltaResponseModel delta)> ReviewWithDeltaAsync()
    {
        return _codeReviewer.ReviewWithDeltaAsync(NextPath("review-with-delta"), BenchmarkInputs.CurrentCode);
    }

    [Benchmark]
    public Task<Codescene.VSExtension.Core.Models.Cli.Delta.DeltaResponseModel> DeltaAsyncWithRefactorDiscovery()
    {
        return _codeReviewerWithPreflight.DeltaAsync(
            new FileReviewModel { FilePath = NextPath("delta-refactor"), RawScore = _currentRawScore },
            BenchmarkInputs.CurrentCode);
    }

    private string NextPath(string prefix)
    {
        var index = Interlocked.Increment(ref _pathCounter);
        return Path.Combine(_environment.RootDirectory, prefix + "-" + index + ".cs");
    }
}
