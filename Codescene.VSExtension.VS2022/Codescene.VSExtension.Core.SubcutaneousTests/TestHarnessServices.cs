// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public sealed class TestLogger : ILogger
{
    private readonly EventJournal _journal;

    public TestLogger(EventJournal journal)
    {
        _journal = journal;
    }

    public void Debug(string message)
    {
    }

    public void Error(string message, Exception ex)
    {
        _journal.Record("log.error", detail: $"{message} | {ex.Message}");
    }

    public void Info(string message, bool statusBar = false)
    {
    }

    public void Warn(string message, bool statusBar = false)
    {
        _journal.Record("log.warn", detail: message);
    }
}

public sealed class TestSavedFilesTracker : ISavedFilesTracker
{
    private readonly HashSet<string> _savedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly EventJournal _journal;

    public TestSavedFilesTracker(EventJournal journal)
    {
        _journal = journal;
    }

    public IEnumerable<string> GetSavedFiles()
    {
        lock (_savedFiles)
        {
            return _savedFiles.ToArray();
        }
    }

    public void ClearSavedFiles()
    {
        lock (_savedFiles)
        {
            _savedFiles.Clear();
        }
    }

    public void RemoveFromTracker(string filePath)
    {
        lock (_savedFiles)
        {
            _savedFiles.Remove(filePath);
        }
    }

    public void MarkSaved(string filePath)
    {
        lock (_savedFiles)
        {
            _savedFiles.Add(filePath);
        }

        _journal.Record("saved-file.added", filePath);
    }
}

public sealed class TestOpenFilesObserver : IOpenFilesObserver
{
    public IEnumerable<string> GetAllVisibleFileNames()
    {
        return Array.Empty<string>();
    }

    public string GetActiveDocumentPath()
    {
        return string.Empty;
    }
}

public sealed class TestSupportedFileChecker : ISupportedFileChecker
{
    private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".ts",
        ".js",
        ".tsx",
        ".jsx",
        ".py",
    };

    public bool IsSupported(string filePath)
    {
        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }
}

public sealed class TestCliServices : ICliServices
{
    public TestCliServices(ICliCommandProvider commandProvider, IProcessExecutor processExecutor, ICacheStorageService cacheStorage)
    {
        CommandProvider = commandProvider;
        ProcessExecutor = processExecutor;
        CacheStorage = cacheStorage;
    }

    public ICliCommandProvider CommandProvider { get; }

    public IProcessExecutor ProcessExecutor { get; }

    public ICacheStorageService CacheStorage { get; }
}

public sealed class TestCacheStorageService : ICacheStorageService
{
    private readonly string _cacheDirectory;

    public TestCacheStorageService(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_cacheDirectory);
        return Task.CompletedTask;
    }

    public string GetSolutionReviewCacheLocation()
    {
        return _cacheDirectory;
    }

    public void RemoveOldReviewCacheEntries(int nrOfDays = 30)
    {
    }
}

public sealed class TestSettingsProvider : ISettingsProvider
{
    public bool ShowDebugLogs => false;

    public string AuthToken => string.Empty;
}
