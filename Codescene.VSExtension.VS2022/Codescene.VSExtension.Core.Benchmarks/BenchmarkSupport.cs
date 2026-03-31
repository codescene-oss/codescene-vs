// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using AutoRefactorConfig = Codescene.VSExtension.Core.Models.WebComponent.Data.AutoRefactorConfig;

namespace Codescene.VSExtension.Core.Benchmarks;

internal sealed class BenchmarkEnvironment : IDisposable
{
    private readonly NoopLogger _logger = new NoopLogger();
    private readonly ICliSettingsProvider _cliSettingsProvider = new CliSettingsProvider();
    private readonly ModelMapper _modelMapper = new ModelMapper();
    private readonly FixedSettingsProvider _settingsProvider = new FixedSettingsProvider();
    private readonly IProcessExecutor _processExecutor;
    private readonly ICliCommandProvider _commandProvider;
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

        _cacheStorageService = new BenchmarkCacheStorageService(CacheDirectory);
        _commandProvider = new CliCommandProvider(new CliObjectScoreCreator(_logger));
        _processExecutor = CreateProcessExecutor(_cliSettingsProvider, _logger);
    }

    public string RootDirectory { get; }

    public string CacheDirectory { get; }

    public string ExistingFilePath { get; }

    public CliExecutor CreateCliExecutor()
    {
        return new CliExecutor(
            _logger,
            new BenchmarkCliServices(_commandProvider, _processExecutor, _cacheStorageService),
            _settingsProvider,
            null);
    }

    public CodeReviewer CreateCodeReviewer(IGitService gitService, IPreflightManager? preflightManager = null)
    {
        return new CodeReviewer(
            _logger,
            _modelMapper,
            CreateCliExecutor(),
            null,
            gitService,
            null,
            preflightManager);
    }

    public CachingCodeReviewer CreateCachingCodeReviewer(IGitService gitService, IPreflightManager? preflightManager = null)
    {
        return new CachingCodeReviewer(
            CreateCodeReviewer(gitService, preflightManager),
            new ReviewCacheService(new ConcurrentDictionary<string, ReviewCacheItem>()),
            new BaselineReviewCacheService(new ConcurrentDictionary<string, string>()),
            new DeltaCacheService(new ConcurrentDictionary<string, DeltaCacheItem>()),
            _logger,
            gitService,
            null,
            null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Directory.Delete(RootDirectory, true);
        }
        catch
        {
        }
    }

    private static IProcessExecutor CreateProcessExecutor(ICliSettingsProvider cliSettingsProvider, ILogger logger)
    {
        var processExecutorType = typeof(CliSettingsProvider).Assembly.GetType(
            "Codescene.VSExtension.Core.Application.Cli.ProcessExecutor",
            throwOnError: true);
        var processExecutor = (IProcessExecutor?)Activator.CreateInstance(
            processExecutorType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { cliSettingsProvider, logger },
            null);
        return processExecutor!;
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

internal sealed class BenchmarkCliServices : ICliServices
{
    public BenchmarkCliServices(ICliCommandProvider commandProvider, IProcessExecutor processExecutor, ICacheStorageService cacheStorage)
    {
        CommandProvider = commandProvider;
        ProcessExecutor = processExecutor;
        CacheStorage = cacheStorage;
    }

    public ICliCommandProvider CommandProvider { get; }

    public IProcessExecutor ProcessExecutor { get; }

    public ICacheStorageService CacheStorage { get; }
}

internal sealed class BenchmarkCacheStorageService : ICacheStorageService
{
    public BenchmarkCacheStorageService(string cacheDirectory)
    {
        CacheDirectory = cacheDirectory;
    }

    public string CacheDirectory { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(CacheDirectory);
        return Task.CompletedTask;
    }

    public string GetSolutionReviewCacheLocation() => CacheDirectory;

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
}

internal sealed class FixedGitService : IGitService
{
    private readonly string _defaultBaselineContent;
    private readonly Dictionary<string, string> _pathContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public FixedGitService(string defaultBaselineContent)
    {
        _defaultBaselineContent = defaultBaselineContent;
    }

    public void SetContent(string path, string content)
    {
        _pathContents[path] = content;
    }

    public string GetFileContentForCommit(string path)
    {
        if (path != null && _pathContents.TryGetValue(path, out var content))
        {
            return content;
        }

        return _defaultBaselineContent;
    }

    public bool IsFileIgnored(string filePath) => false;

    public void Dispose()
    {
    }
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
