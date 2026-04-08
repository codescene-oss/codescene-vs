// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using System.Reflection;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.Application.Git;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public abstract class SubcutaneousGitTestBase
{
    protected EventJournal Journal { get; private set; } = null!;

    protected TestLogger Logger { get; private set; } = null!;

    protected RecordingAsyncTaskScheduler TaskScheduler { get; private set; } = null!;

    protected TestSavedFilesTracker SavedFilesTracker { get; private set; } = null!;

    protected TestOpenFilesObserver OpenFilesObserver { get; private set; } = null!;

    protected TestSupportedFileChecker SupportedFileChecker { get; private set; } = null!;

    protected RecordingCliExecutor CliExecutor { get; private set; } = null!;

    protected RecordingCodeReviewer CodeReviewer { get; private set; } = null!;

    protected RecordingGitChangeLister GitChangeLister { get; private set; } = null!;

    protected RecordingGitChangeObserverCore Observer { get; private set; } = null!;

    protected GitService GitService { get; private set; } = null!;

    protected DeltaCacheService DeltaCache { get; private set; } = null!;

    protected string RepositoryRoot { get; private set; } = string.Empty;

    protected string CacheDirectory { get; private set; } = string.Empty;

    protected virtual int GitPollingIntervalSeconds => 1;

    protected virtual bool AutoStartObserver => true;

    protected virtual int DefaultTimeoutMs => 30000;

    protected virtual int ConditionPollIntervalMs => 100;

    [TestInitialize]
    public virtual async Task InitializeAsync()
    {
        CacheGeneration.Reset();
        DeltaJobTracker.Clear();
        Journal = new EventJournal();
        Logger = new TestLogger(Journal);
        TaskScheduler = new RecordingAsyncTaskScheduler(Journal);
        SavedFilesTracker = new TestSavedFilesTracker(Journal);
        OpenFilesObserver = new TestOpenFilesObserver();
        SupportedFileChecker = new TestSupportedFileChecker();
        DeltaCache = new DeltaCacheService();

        RepositoryRoot = Path.Combine(Path.GetTempPath(), "codescene-subcutaneous-tests", Guid.NewGuid().ToString("N"));
        CacheDirectory = Path.Combine(RepositoryRoot, ".cache");
        Directory.CreateDirectory(RepositoryRoot);
        Directory.CreateDirectory(CacheDirectory);

        InitializeGitRepository();

        GitService = new GitService(Logger);
        CliExecutor = new RecordingCliExecutor(CreateCliExecutor(), Journal);

        var notifier = new CodeHealthMonitorNotifier();
        notifier.ViewUpdateRequested += (_, _) => Journal.Record("notifier.view-update");

        var innerReviewer = new CodeReviewer(
            Logger,
            new ModelMapper(),
            CliExecutor,
            null,
            GitService,
            notifier,
            null);

        var cachingReviewer = new CachingCodeReviewer(
            innerReviewer,
            logger: Logger,
            git: GitService,
            notifier: notifier,
            deltaCache: DeltaCache);

        CodeReviewer = new RecordingCodeReviewer(cachingReviewer, Journal);
        GitChangeLister = new RecordingGitChangeLister(
            new GitChangeLister(SavedFilesTracker, SupportedFileChecker, Logger, GitService, pollingInterval: GitPollingIntervalSeconds),
            Journal);

        Observer = new RecordingGitChangeObserverCore(
            Logger,
            CodeReviewer,
            SupportedFileChecker,
            TaskScheduler,
            GitChangeLister,
            GitService,
            Journal);

        Observer.FileDeletedFromGit += (_, path) => Journal.Record("observer.file-deleted", path);
        Observer.ViewUpdateRequested += (_, _) => Journal.Record("observer.view-update");
        Observer.Initialize(RepositoryRoot, SavedFilesTracker, OpenFilesObserver);

        if (AutoStartObserver)
        {
            await StartObserverAsync();
        }
    }

    [TestCleanup]
    public virtual async Task CleanupAsync()
    {
        if (TaskScheduler != null)
        {
            await TaskScheduler.WaitForIdleAsync(DefaultTimeoutMs);
        }

        Observer?.Dispose();
        GitChangeLister?.Dispose();
        GitService?.Dispose();
        TaskScheduler?.Dispose();

        if (!string.IsNullOrEmpty(RepositoryRoot) && Directory.Exists(RepositoryRoot))
        {
            ReviewCacheCleanup.CleanupCaches(RepositoryRoot);

            try
            {
                Directory.Delete(RepositoryRoot, recursive: true);
            }
            catch
            {
            }
        }

        CacheGeneration.Reset();
        DeltaJobTracker.Clear();
    }

    protected string AbsolutePath(string relativePath)
    {
        return Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    protected async Task StartObserverAsync()
    {
        Observer.Start();
        await WaitForConditionAsync(
            () => Observer.FileWatcher != null && Observer.FileWatcher.EnableRaisingEvents,
            "The git observer did not finish starting.");
    }

    protected Task<string> WriteWorkingFileAsync(string relativePath, string content, bool markSaved = false)
    {
        var absolutePath = AbsolutePath(relativePath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        System.IO.File.WriteAllText(absolutePath, content);
        if (markSaved)
        {
            SavedFilesTracker.MarkSaved(absolutePath);
        }

        Journal.Record("stimulus.file-write", absolutePath, $"length={content.Length}");
        return Task.FromResult(absolutePath);
    }

    protected Task AppendWorkingFileAsync(string relativePath, string content)
    {
        var absolutePath = AbsolutePath(relativePath);
        System.IO.File.AppendAllText(absolutePath, content);
        Journal.Record("stimulus.file-append", absolutePath, $"length={content.Length}");
        return Task.CompletedTask;
    }

    protected void DeleteWorkingFile(string relativePath)
    {
        var absolutePath = AbsolutePath(relativePath);
        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
        }

        Journal.Record("stimulus.file-delete", absolutePath);
    }

    protected void ExecGit(string arguments)
    {
        var result = RunGitCommand(arguments);
        EnsureGitSucceeded(arguments, result);
    }

    protected GitCommandResult ExecGitAllowFailure(string arguments)
    {
        return RunGitCommand(arguments);
    }

    protected void CommitAll(string message)
    {
        ExecGit("add -A");
        ExecGit($"commit -m \"{message}\"");
    }

    protected async Task<string> CreateCommittedFileAsync(string relativePath, string content, string commitMessage)
    {
        var absolutePath = await WriteWorkingFileAsync(relativePath, content);
        CommitAll(commitMessage);
        Journal.Record("stimulus.file-committed", absolutePath, commitMessage);
        return absolutePath;
    }

    protected void CheckoutBranch(string branchName, bool create = false)
    {
        ExecGit(create ? $"checkout -b {branchName}" : $"checkout {branchName}");
    }

    protected void ResetHard(string target)
    {
        ExecGit($"reset --hard {target}");
    }

    protected void StashPush(string name, bool includeUntracked = false)
    {
        var includeUntrackedArg = includeUntracked ? " -u" : string.Empty;
        ExecGit($"stash push{includeUntrackedArg} -m \"{name}\"");
    }

    protected void StashPop()
    {
        ExecGit("stash pop");
    }

    protected GitCommandResult StashPopAllowFailure()
    {
        return ExecGitAllowFailure("stash pop");
    }

    protected void RebaseOnto(string target)
    {
        ExecGit($"rebase {target}");
    }

    protected bool IsTracked(string relativePath)
    {
        return Observer.GetTrackerManager().Contains(AbsolutePath(relativePath));
    }

    protected bool HasDelta(string relativePath)
    {
        var abs = AbsolutePath(relativePath);
        return DeltaCache.GetAll().Keys.Any(k => string.Equals(k, abs, StringComparison.OrdinalIgnoreCase));
    }

    protected int ReviewCount(string relativePath)
    {
        return CodeReviewer.GetReviewWithDeltaCallCount(AbsolutePath(relativePath));
    }

    protected int MaxParallelReviews(string relativePath)
    {
        return CodeReviewer.GetMaxParallelism(AbsolutePath(relativePath));
    }

    protected int RunningDeltaJobCount()
    {
        return DeltaJobTracker.RunningJobs.Count;
    }

    protected bool HasRunningDeltaJob(string relativePath)
    {
        var absolutePath = AbsolutePath(relativePath);
        return DeltaJobTracker.RunningJobs.Any(job => string.Equals(job.File?.FileName, absolutePath, StringComparison.OrdinalIgnoreCase));
    }

    protected IReadOnlyCollection<string> DeltaCachePaths()
    {
        return DeltaCache.GetAll().Keys.ToList().AsReadOnly();
    }

    protected async Task WaitForTrackedAsync(string relativePath, string? message = null, int timeoutMs = 30000)
    {
        await WaitForConditionAsync(() => IsTracked(relativePath), message ?? $"Expected {relativePath} to be tracked.", timeoutMs);
    }

    protected async Task WaitForNotTrackedAsync(string relativePath, string? message = null, int timeoutMs = 30000)
    {
        await WaitForConditionAsync(() => !IsTracked(relativePath), message ?? $"Expected {relativePath} to be removed from the tracker.", timeoutMs);
    }

    protected async Task WaitForDeltaAsync(string relativePath, string? message = null, int timeoutMs = 30000)
    {
        await WaitForConditionAsync(() => HasDelta(relativePath), message ?? $"Expected {relativePath} to produce a delta result.", timeoutMs);
    }

    protected async Task WaitForNoDeltaAsync(string relativePath, string? message = null, int timeoutMs = 30000)
    {
        await WaitForConditionAsync(() => !HasDelta(relativePath), message ?? $"Expected {relativePath} to be absent from the delta cache.", timeoutMs);
    }

    protected async Task WaitForReviewCountAsync(string relativePath, int expectedCount, string? message = null, int timeoutMs = 30000)
    {
        await WaitForConditionAsync(
            () => ReviewCount(relativePath) >= expectedCount,
            message ?? $"Expected at least {expectedCount} review(s) for {relativePath}.",
            timeoutMs);
    }

    protected void SnapshotState(string label, params string[] relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var absolutePath = AbsolutePath(relativePath);
            Journal.Record(
                "state.snapshot",
                absolutePath,
                $"{label};tracked={Observer.GetTrackerManager().Contains(absolutePath)};delta={HasDelta(relativePath)};reviewCount={CodeReviewer.GetReviewWithDeltaCallCount(absolutePath)}");
        }
    }

    protected async Task WaitForConditionAsync(Func<bool> condition, string failureMessage, int timeoutMs = 30000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(ConditionPollIntervalMs);
        }

        Assert.Fail($"{failureMessage}{Environment.NewLine}{Environment.NewLine}{Journal.Dump()}");
    }

    private GitCommandResult RunGitCommand(string arguments)
    {
        Journal.Record("stimulus.git", detail: arguments);
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        process!.WaitForExit();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Journal.Record("stimulus.git.completed", detail: $"{arguments} => exit={process.ExitCode}");
        return new GitCommandResult(process.ExitCode, stdout, stderr);
    }

    private void EnsureGitSucceeded(string arguments, GitCommandResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new AssertFailedException(
                $"Git command failed: git {arguments}{Environment.NewLine}stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{result.StandardError}");
        }
    }

    private CliExecutor CreateCliExecutor()
    {
        var settingsProvider = new TestSettingsProvider();
        var cliSettingsProvider = new CliSettingsProvider();
        var commandProvider = new CliCommandProvider(new CliObjectScoreCreator(Logger));
        var processExecutor = CreateProcessExecutor(cliSettingsProvider);
        var cliServices = new TestCliServices(commandProvider, processExecutor, new TestCacheStorageService(CacheDirectory));
        return new CliExecutor(Logger, cliServices, settingsProvider, null);
    }

    private IProcessExecutor CreateProcessExecutor(ICliSettingsProvider cliSettingsProvider)
    {
        var processExecutorType = typeof(CliSettingsProvider).Assembly.GetType(
            "Codescene.VSExtension.Core.Application.Cli.ProcessExecutor",
            throwOnError: true);

        var processExecutor = Activator.CreateInstance(
            processExecutorType!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { cliSettingsProvider, Logger },
            null);

        return (IProcessExecutor)processExecutor!;
    }

    private void InitializeGitRepository()
    {
        ExecGit("init");
        ExecGit("branch -M main");
        ExecGit("config user.email test@example.com");
        ExecGit("config user.name \"Test User\"");
        var readmePath = AbsolutePath("README.md");
        System.IO.File.WriteAllText(readmePath, "# Subcutaneous Test Repository");
        ExecGit("add README.md");
        ExecGit("commit -m \"Initial commit\"");
    }
}

public sealed class GitCommandResult
{
    public GitCommandResult(int exitCode, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }
}
