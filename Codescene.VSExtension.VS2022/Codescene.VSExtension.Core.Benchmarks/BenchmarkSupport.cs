// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using AutoRefactorConfig = Codescene.VSExtension.Core.Models.WebComponent.Data.AutoRefactorConfig;

namespace Codescene.VSExtension.Core.Benchmarks;

internal sealed class BenchmarkEnvironment : IDisposable
{
    private readonly NoopLogger _logger = new NoopLogger();
    private readonly ICliSettingsProvider _cliSettingsProvider = new CliSettingsProvider();
    private readonly ModelMapper _modelMapper = new ModelMapper();
    private readonly FixedSettingsProvider _settingsProvider = new FixedSettingsProvider();
    private readonly IIdeServerClient _ideServerClient;
    private readonly BenchmarkCacheStorageService _cacheStorageService;
    private bool _disposed;

    public BenchmarkEnvironment()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), "codescene-benchmarks", Guid.NewGuid().ToString("N"));
        CacheDirectory = Path.Combine(RootDirectory, "cache");
        ExistingFilePath = Path.Combine(RootDirectory, "BenchmarkedFile.cs");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(CacheDirectory);
        System.IO.File.WriteAllText(ExistingFilePath, BenchmarkInputs.CurrentCode);

        _cacheStorageService = new BenchmarkCacheStorageService(CacheDirectory, RootDirectory);
        _ideServerClient = new IdeServerClient(_cliSettingsProvider, _logger, _settingsProvider);
    }

    public string RootDirectory { get; }

    public string CacheDirectory { get; }

    public string ExistingFilePath { get; }

    public CliExecutor CreateCliExecutor()
    {
        return new CliExecutor(
            _logger,
            _ideServerClient,
            _cacheStorageService,
            _settingsProvider,
            null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ideServerClient.Dispose();
        try
        {
            Directory.Delete(RootDirectory, true);
        }
        catch
        {
        }
    }
}

internal static class BenchmarkInputs
{
    public const string FileName = "BenchmarkedFile.cs";
    public const string BaselineCode = @"
public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";

    public const string CurrentCode = @"
public class ComplexProcessor
{
    public int Calculate(int a, int b, int c, int d, int e, int f, int g, int h)
    {
        if (a > 0)
        {
            if (b > 0)
            {
                if (c > 0)
                {
                    if (d > 0)
                    {
                        if (e > 0)
                        {
                            return a + b + c + d + e + f + g + h;
                        }
                    }
                }
            }
        }

        return 0;
    }
}";
}

internal sealed class BenchmarkCacheStorageService : ICacheStorageService
{
    public BenchmarkCacheStorageService(string cacheDirectory, string workspaceDirectory)
    {
        CacheDirectory = cacheDirectory;
        WorkspaceDirectory = workspaceDirectory;
    }

    public string CacheDirectory { get; }

    public string WorkspaceDirectory { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(CacheDirectory);
        return Task.CompletedTask;
    }

    public string GetSolutionReviewCacheLocation() => CacheDirectory;

    public string GetWorkspaceDirectory() => WorkspaceDirectory ?? string.Empty;

    public void RemoveOldReviewCacheEntries(int nrOfDays = 30)
    {
    }
}

internal sealed class NoopLogger : ILogger
{
    public void Debug(string message)
    {
    }

    public void Error(string message, Exception ex)
    {
    }

    public void Info(string message, bool statusBar = false)
    {
    }

    public void Warn(string message, bool statusBar = false)
    {
    }
}

internal sealed class FixedSettingsProvider : ISettingsProvider
{
    public bool ShowDebugLogs => false;

    public string AuthToken => string.Empty;

    public int ServerWorkerThreads => 0;

    public bool EnableAutomaticAnalysis => true;

    public bool EnableTelemetry => true;
}

internal sealed class FixedPreflightManager : IPreflightManager
{
    private readonly PreFlightResponseModel _response;

    public FixedPreflightManager(PreFlightResponseModel response)
    {
        _response = response;
    }

    public bool IsSupportedLanguage(string extension) => true;

    public Task<PreFlightResponseModel> RunPreflightAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_response);
    }

    public Task<PreFlightResponseModel> GetPreflightResponseAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_response);
    }

    public AutoRefactorConfig GetAutoRefactorConfig()
    {
        return new AutoRefactorConfig();
    }

    public void SetHasAceToken(bool hasAceToken)
    {
    }
}
