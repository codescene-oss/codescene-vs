// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.VS2022.Application.Git;

[Export(typeof(IGitService))]
public class GitService : IGitService, IDisposable
{
    private readonly ILogger _logger;

    private readonly LibGit2SharpIgnoreChecker _ignoreChecker;
    private readonly CachingGitIgnoreChecker _cachingIgnoreChecker;
    private FileSystemWatcher _gitignoreWatcher;
    private string _watchedRepoRoot;

    [ImportingConstructor]
    public GitService(ILogger logger)
    {
        _logger = logger;
        _ignoreChecker = new LibGit2SharpIgnoreChecker(logger);
        _cachingIgnoreChecker = new CachingGitIgnoreChecker(_ignoreChecker);
    }

    // TODO: Move to helper
    public static string GetRelativePath(string basePath, string fullPath)
    {
        var baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
        var fullUri = new Uri(fullPath);
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Gets the baseline commit for delta analysis.
    /// - If on a main branch: returns HEAD commit (compare against current committed state)
    /// - If on a feature branch: returns merge-base with main branch.
    /// </summary>
    public string GetBaselineCommit(Repository repository)
    {
        try
        {
            var currentBranchName = repository.Head?.FriendlyName;
            if (string.IsNullOrEmpty(currentBranchName))
            {
                _logger.Warn("Could not determine current branch name.");
                return string.Empty;
            }

            // If on main branch, use HEAD commit as baseline
            if (MainBranchNames.IsMainBranch(currentBranchName))
            {
                var headCommit = repository.Head?.Tip?.Sha ?? string.Empty;
                _logger.Debug($"On main branch '{currentBranchName}', using HEAD as baseline: {headCommit}");
                return headCommit;
            }

            // On feature branch, try to find merge-base with main
            var mergeBase = MainBranchMergeBaseSelector.FindClosest(repository, _logger);
            if (mergeBase != null)
            {
                _logger.Debug($"Using merge-base with main as baseline: {mergeBase.Sha}");
                return mergeBase.Sha;
            }

            // Fallback: try reflog-based approach
            return GetBranchCreationCommitFromReflog(repository);
        }
        catch (Exception e)
        {
            _logger.Error("Could not get baseline commit.", e);
            return string.Empty;
        }
    }

    public string GetFileContentForCommit(string path)
    {
        try
        {
            var repoPath = Repository.Discover(path);
            if (string.IsNullOrEmpty(repoPath))
            {
                _logger.Warn("Repository path is null. Aborting retrieval of file content for specific commit.");
                return string.Empty;
            }

            using var repo = new Repository(repoPath);
            var commitHash = GetBaselineCommit(repo);

            if (string.IsNullOrEmpty(commitHash))
            {
                _logger.Debug("No baseline commit found, skipping file content retrieval.");
                return string.Empty;
            }

            var repoRoot = repo.Info.WorkingDirectory;
            var relativePath = GetRelativePath(repoRoot, path).Replace("\\", "/");

            var commit = repo.Lookup<Commit>(commitHash);
            if (commit == null)
            {
                _logger.Warn($"Could not find commit {commitHash}");
                return string.Empty;
            }

            var entry = commit[relativePath];
            if (entry == null)
            {
                _logger.Debug($"File {relativePath} not found in commit {commitHash}");
                return string.Empty;
            }

            var blob = (Blob)entry.Target;
            return blob.GetContentText();
        }
        catch (Exception e)
        {
            _logger.Warn($"Could not get file content for baseline commit: {e.Message}");
            return string.Empty;
        }
    }

    public bool IsFileIgnored(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.Warn("File path is empty. Aborting ignore checking.");
            return false;
        }

        try
        {
            var repoRoot = _ignoreChecker.GetRepositoryRoot(filePath);
            if (!string.IsNullOrEmpty(repoRoot))
            {
                EnsureWatcherInitialized(repoRoot);
            }

            return _cachingIgnoreChecker.IsPathIgnored(filePath);
        }
        catch (Exception ex)
        {
            _logger.Error($"Could not check if file is ignored: {ex.Message}", ex);
            return false;
        }
    }

    public void Dispose()
    {
        _gitignoreWatcher?.Dispose();
        _gitignoreWatcher = null;
    }

    // TODO: Move to helper
    private static string AppendDirectorySeparatorChar(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }

    private string GetBranchCreationCommitFromReflog(Repository repository)
    {
        try
        {
            var reflog = repository.Refs.Log(repository.Head.CanonicalName);

            var creationEntry = reflog
                .Reverse()
                .FirstOrDefault(entry =>
                    entry.Message != null &&
                    entry.Message.IndexOf("created from", StringComparison.OrdinalIgnoreCase) >= 0);

            return creationEntry?.To.Sha ?? string.Empty;
        }
        catch (Exception e)
        {
            _logger.Debug($"Could not get branch creation from reflog: {e.Message}");
            return string.Empty;
        }
    }

    private void EnsureWatcherInitialized(string repoRoot)
    {
        if (_watchedRepoRoot == repoRoot && _gitignoreWatcher != null)
        {
            return;
        }

        _gitignoreWatcher?.Dispose();
        _watchedRepoRoot = repoRoot;

        if (string.IsNullOrEmpty(repoRoot) || !Directory.Exists(repoRoot))
        {
            return;
        }

        _gitignoreWatcher = new FileSystemWatcher(repoRoot)
        {
            Filter = ".gitignore",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };

        _gitignoreWatcher.Changed += OnGitignoreChanged;
        _gitignoreWatcher.Created += OnGitignoreChanged;
        _gitignoreWatcher.Deleted += OnGitignoreChanged;
        _gitignoreWatcher.EnableRaisingEvents = true;
    }

    private void OnGitignoreChanged(object sender, FileSystemEventArgs e)
    {
        ClearCache();
    }

    private void ClearCache()
    {
        _cachingIgnoreChecker.ClearCache();
    }
}
