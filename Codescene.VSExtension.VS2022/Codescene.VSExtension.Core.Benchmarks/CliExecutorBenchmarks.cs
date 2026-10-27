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
    private DeltaResponseModel _deltaResponse = null!;
    private PreFlightResponseModel _preflightResponse = null!;

    [GlobalSetup]
    public void Setup()
    {
        _environment = new BenchmarkEnvironment();
        _cliExecutor = _environment.CreateCliExecutor();

        _deltaResponse = new DeltaResponseModel { NewScore = 8.5m, OldScore = 7.0m, ScoreChange = 1.5m };

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
    public Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync()
    {
        return _cliExecutor.FnsToRefactorFromDeltaAsync(
            BenchmarkInputs.FileName,
            BenchmarkInputs.CurrentCode,
            _deltaResponse,
            _preflightResponse);
    }
}
