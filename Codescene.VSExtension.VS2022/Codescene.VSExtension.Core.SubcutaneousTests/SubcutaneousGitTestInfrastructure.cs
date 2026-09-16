// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.Application.Git;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

public abstract class SubcutaneousGitTestBase
{
    private IdeServerClient? _ideServerClient;

    protected EventJournal Journal { get; private set; } = null!;

    protected TestLogger Logger { get; private set; } = null!;

    protected RecordingCliExecutor CliExecutor { get; private set; } = null!;

    protected RecordingCodeReviewer CodeReviewer { get; private set; } = null!;

    protected GitService GitService { get; private set; } = null!;

    protected DeltaCacheService DeltaCache { get; private set; } = null!;

    protected string RepositoryRoot { get; private set; } = string.Empty;

    protected string CacheDirectory { get; private set; } = string.Empty;

    protected virtual int DefaultTimeoutMs => 30000;

    [TestInitialize]
    public virtual Task InitializeAsync()
    {
        CacheGeneration.Reset();
        DeltaJobTracker.Clear();
        MainBranchNames.ClearDefaultBranchCache();
        Journal = new EventJournal();
        Logger = new TestLogger(Journal);
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

        CodeReviewer = new RecordingCodeReviewer(innerReviewer, Journal);
        return Task.CompletedTask;
    }

    [TestCleanup]
    public virtual Task CleanupAsync()
    {
        GitService?.Dispose();
        _ideServerClient?.Dispose();

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
        return Task.CompletedTask;
    }

    protected string AbsolutePath(string relativePath)
    {
        return Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    protected Task<string> WriteWorkingFileAsync(string relativePath, string content)
    {
        var absolutePath = AbsolutePath(relativePath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        System.IO.File.WriteAllText(absolutePath, content);
        Journal.Record("stimulus.file-write", absolutePath, $"length={content.Length}");
        return Task.FromResult(absolutePath);
    }

    protected void ExecGit(string arguments)
    {
        var result = RunGitCommand(arguments);
        EnsureGitSucceeded(arguments, result);
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
        var cacheStorage = new TestCacheStorageService(CacheDirectory);
        _ideServerClient = new IdeServerClient(cliSettingsProvider, Logger, settingsProvider);
        return new CliExecutor(Logger, _ideServerClient, cacheStorage, settingsProvider, null);
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
