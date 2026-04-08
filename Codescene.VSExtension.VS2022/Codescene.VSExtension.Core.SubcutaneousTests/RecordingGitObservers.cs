// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public sealed class RecordingGitChangeLister : IGitChangeLister, IDisposable
{
    private readonly IGitChangeLister _inner;
    private readonly EventJournal _journal;

    public RecordingGitChangeLister(IGitChangeLister inner, EventJournal journal)
    {
        _inner = inner;
        _journal = journal;
        _inner.FilesDetected += OnFilesDetected;
    }

    public event EventHandler<HashSet<string>>? FilesDetected;

    public async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath, CancellationToken cancellationToken = default)
    {
        _journal.Record("lister.changed-files.started");
        var files = await _inner.GetAllChangedFilesAsync(gitRootPath, workspacePath, cancellationToken);
        _journal.Record("lister.changed-files.completed", detail: $"count={files.Count}");
        return files;
    }

    public Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath, CancellationToken cancellationToken = default)
    {
        return _inner.GetChangedFilesVsMergeBaseAsync(gitRootPath, workspacePath, cancellationToken);
    }

    public void Initialize(string gitRootPath, IReadOnlyCollection<string> workspacePaths)
    {
        _journal.Record("lister.initialize", gitRootPath, $"workspaceCount={workspacePaths.Count}");
        _inner.Initialize(gitRootPath, workspacePaths);
    }

    public void SetWorkspacePaths(IReadOnlyCollection<string> workspacePaths)
    {
        _inner.SetWorkspacePaths(workspacePaths);
    }

    public void StartPeriodicScanning(CancellationToken cancellationToken)
    {
        _journal.Record("lister.periodic.start");
        _inner.StartPeriodicScanning(cancellationToken);
    }

    public void StopPeriodicScanning()
    {
        _journal.Record("lister.periodic.stop");
        _inner.StopPeriodicScanning();
    }

    public async Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default)
    {
        _journal.Record("lister.collect.started");
        var files = await _inner.CollectFilesFromRepoStateAsync(gitRootPath, workspacePaths, cancellationToken);
        _journal.Record("lister.collect.completed", detail: $"count={files.Count}");
        return files;
    }

    public void Dispose()
    {
        _inner.FilesDetected -= OnFilesDetected;
        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void OnFilesDetected(object? sender, HashSet<string> files)
    {
        _journal.Record("lister.files-detected", detail: $"count={files.Count}");
        FilesDetected?.Invoke(this, files);
    }
}

public sealed class RecordingGitChangeObserverCore : GitChangeObserverCore
{
    private readonly EventJournal _journal;

    public RecordingGitChangeObserverCore(
        ILogger logger,
        ICodeReviewer codeReviewer,
        ISupportedFileChecker supportedFileChecker,
        IAsyncTaskScheduler taskScheduler,
        IGitChangeLister gitChangeLister,
        IGitService gitService,
        EventJournal journal)
        : base(logger, codeReviewer, supportedFileChecker, taskScheduler, gitChangeLister, gitService)
    {
        _journal = journal;
    }

    public override async Task<List<string>> GetChangedFilesVsBaselineAsync()
    {
        _journal.Record("observer.changed-files.started");
        var files = await base.GetChangedFilesVsBaselineAsync();
        _journal.Record("observer.changed-files.completed", detail: $"count={files.Count}");
        return files;
    }
}
