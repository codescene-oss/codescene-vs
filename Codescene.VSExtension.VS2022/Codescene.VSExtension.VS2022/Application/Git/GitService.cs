// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
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
    private FileSystemWatcher _globalExcludesWatcher;
    private string _watchedRepoRoot;
    private string _watchedGlobalExcludesPath;

    [ImportingConstructor]
    public GitService(ILogger logger)
    {
        _logger = logger;
        _ignoreChecker = new LibGit2SharpIgnoreChecker(logger);
        _cachingIgnoreChecker = new CachingGitIgnoreChecker(_ignoreChecker);
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
        _globalExcludesWatcher?.Dispose();
        _globalExcludesWatcher = null;
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path.Substring(2));
        }

        return path;
    }

    private void EnsureWatcherInitialized(string repoRoot)
    {
        if (_watchedRepoRoot == repoRoot && _gitignoreWatcher != null)
        {
            return;
        }

        _gitignoreWatcher?.Dispose();
        _globalExcludesWatcher?.Dispose();
        _watchedRepoRoot = repoRoot;
        _watchedGlobalExcludesPath = null;

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

        EnsureGlobalExcludesWatcherInitialized(repoRoot);
    }

    private void EnsureGlobalExcludesWatcherInitialized(string repoRoot)
    {
        try
        {
            var repoPath = Repository.Discover(repoRoot);
            if (string.IsNullOrEmpty(repoPath))
            {
                return;
            }

            string globalExcludesPath;
            using (var repo = new Repository(repoPath))
            {
                globalExcludesPath = repo.Config.Get<string>("core.excludesfile")?.Value;
            }

            if (string.IsNullOrEmpty(globalExcludesPath))
            {
                return;
            }

            globalExcludesPath = ResolvePath(globalExcludesPath);

            if (_watchedGlobalExcludesPath == globalExcludesPath && _globalExcludesWatcher != null)
            {
                return;
            }

            _watchedGlobalExcludesPath = globalExcludesPath;
            _globalExcludesWatcher?.Dispose();

            var directory = Path.GetDirectoryName(globalExcludesPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            var fileName = Path.GetFileName(globalExcludesPath);
            _globalExcludesWatcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            };

            _globalExcludesWatcher.Changed += OnGitignoreChanged;
            _globalExcludesWatcher.Created += OnGitignoreChanged;
            _globalExcludesWatcher.Deleted += OnGitignoreChanged;
            _globalExcludesWatcher.Renamed += OnGitignoreRenamed;
            _globalExcludesWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger.Debug($"Could not initialize global excludes watcher: {ex.Message}");
        }
    }

    private void OnGitignoreChanged(object sender, FileSystemEventArgs e)
    {
        ClearCache();
    }

    private void OnGitignoreRenamed(object sender, RenamedEventArgs e)
    {
        ClearCache();
    }

    private void ClearCache()
    {
        _cachingIgnoreChecker.ClearCache();
    }
}
