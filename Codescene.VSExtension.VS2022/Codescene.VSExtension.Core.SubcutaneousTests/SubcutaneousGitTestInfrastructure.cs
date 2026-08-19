// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Cli.Rpc;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.Application.Git;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public abstract class SubcutaneousGitTestBase
{
    private static readonly object HostSync = new object();
    private static IIdeServerHost? _sharedHost;

    protected EventJournal Journal { get; private set; } = null!;

    protected TestLogger Logger { get; private set; } = null!;

    protected RecordingAsyncTaskScheduler TaskScheduler { get; private set; } = null!;

    protected TestSavedFilesTracker SavedFilesTracker { get; private set; } = null!;

    protected TestOpenFilesObserver OpenFilesObserver { get; private set; } = null!;

    protected TestSupportedFileChecker SupportedFileChecker { get; private set; } = null!;

    protected RecordingCliExecutor CliExecutor { get; private set; } = null!;

    protected RecordingCodeReviewer CodeReviewer { get; private set; } = null!;

    protected GitService GitService { get; private set; } = null!;

    protected DeltaCacheService DeltaCache { get; private set; } = null!;

    protected string RepositoryRoot { get; private set; } = string.Empty;

    protected string CacheDirectory { get; private set; } = string.Empty;

    protected virtual int GitPollingIntervalSeconds => 1;

    protected virtual int DefaultTimeoutMs => 30000;

    protected virtual int ConditionPollIntervalMs => 100;

    [TestInitialize]
    public virtual async Task InitializeAsync()
    {
        CacheGeneration.Reset();
        DeltaJobTracker.Clear();
        MainBranchNames.ClearDefaultBranchCache();
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
    }

    [TestCleanup]
    public virtual async Task CleanupAsync()
    {
        if (TaskScheduler != null)
        {
            await TaskScheduler.WaitForIdleAsync(DefaultTimeoutMs);
        }

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

    protected void SetOriginHeadToBranch(string branchName)
    {
        ExecGit($"update-ref refs/remotes/origin/{branchName} refs/heads/{branchName}");
        ExecGit($"symbolic-ref refs/remotes/origin/HEAD refs/remotes/origin/{branchName}");
    }

    protected void WriteCodesceneConfig(string baselineBranch)
    {
        var codesceneDir = Path.Combine(RepositoryRoot, CodesceneFileWatcher.CodesceneDir);
        Directory.CreateDirectory(codesceneDir);
        var configPath = Path.Combine(codesceneDir, CodesceneFileWatcher.ConfigFileName);
        var json = $"{{\"baseline_branch\":\"{baselineBranch}\"}}";
        System.IO.File.WriteAllText(configPath, json);
        Journal.Record("stimulus.config-write", configPath, $"baseline_branch={baselineBranch}");
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

    protected void SnapshotState(string label, params string[] relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var absolutePath = AbsolutePath(relativePath);
            Journal.Record(
                "state.snapshot",
                absolutePath,
                $"{label};delta={HasDelta(relativePath)};reviewCount={CodeReviewer.GetReviewWithDeltaCallCount(absolutePath)}");
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
        return new CliExecutor(Logger, EnsureSharedHost(), new TestCacheStorageService(CacheDirectory), settingsProvider, null!);
    }

    private IIdeServerHost EnsureSharedHost()
    {
        lock (HostSync)
        {
            if (_sharedHost?.IsRunning == true)
            {
                return _sharedHost;
            }

            _sharedHost?.Dispose();
            _sharedHost = new IdeServerHost(new CliSettingsProvider(), Logger);
            _sharedHost.StartAsync().GetAwaiter().GetResult();
            return _sharedHost;
        }
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
