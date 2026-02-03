using System;
using System.Diagnostics;
using System.IO;
using LibGit2Sharp;

namespace Codescene.VSExtension.VS2022.Application.Git;

/// <summary>
/// Monitors changes to the current Git branch in the repository associated with a Visual Studio solution.
/// Watches the .git/HEAD file and triggers a callback when a branch switch is detected.
/// </summary>
public class BranchWatcherService : IDisposable
{
    private FileSystemWatcher _headWatcher;
    private string _repoPath;
    private string _gitDirPath;
    private string _headFilePath;
    private string _lastBranch;
    private Action<string> _onBranchChanged;

    /// <summary>
    /// Initializes branch monitoring for the Git repository containing the given solution.
    /// </summary>
    /// <param name="solutionPath">The full path to the solution file or folder.</param>
    /// <param name="onBranchChanged">
    /// A callback that is invoked with the new branch name whenever a branch switch is detected.
    /// </param>
    public void StartWatching(string solutionPath, Action<string> onBranchChanged)
    {
        InitializeGitInformation(solutionPath);

        if (!File.Exists(_headFilePath))
        {
            return;
        }

        _onBranchChanged = onBranchChanged;

        InitializeGitChangeMonitor();
    }

    /// <summary>
    /// Discovers the Git repository for the given solution and sets internal paths for .git tracking.
    /// </summary>
    /// <param name="solutionPath">The full path to the Visual Studio solution or folder.</param>
    private void InitializeGitInformation(string solutionPath)
    {
        _repoPath = Repository.Discover(solutionPath);
        if (string.IsNullOrEmpty(_repoPath))
        {
            Debug.WriteLine("BranchWatcherService: No git repo found.");
            return;
        }

        using var repo = new Repository(_repoPath);

        _gitDirPath = repo.Info.Path;
        _headFilePath = Path.Combine(_gitDirPath, "HEAD");
        _lastBranch = ReadCurrentBranch();
    }

    /// <summary>
    /// Sets up the FileSystemWatcher to monitor the .git/HEAD file for changes.
    /// </summary>
    private void InitializeGitChangeMonitor()
    {
        _headWatcher = new FileSystemWatcher(_gitDirPath)
        {
            Filter = "HEAD",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
        };

        _headWatcher.Changed += OnHeadFileChanged;
        _headWatcher.Created += OnHeadFileChanged;
        _headWatcher.Renamed += OnHeadFileChanged;
        _headWatcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Handles file system events triggered when the HEAD file is modified.
    /// Detects a branch switch and invokes the registered callback if necessary.
    /// </summary>
    private void OnHeadFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var current = ReadCurrentBranch();
            if (current != _lastBranch)
            {
                _lastBranch = current;
                Debug.WriteLine($"BranchWatcherService: Branch changed to {current}");
                _onBranchChanged?.Invoke(current);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BranchWatcherService: Error during HEAD check - {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the current branch from the .git/HEAD file.
    /// </summary>
    /// <returns>The name of the currently checked-out branch, or "(detached)" if in detached HEAD state.</returns>
    private string ReadCurrentBranch()
    {
        if (!File.Exists(_headFilePath))
        {
            return "(missing)";
        }

        var content = File.ReadAllText(_headFilePath).Trim();
        if (content.StartsWith("ref:"))
        {
            return content.Replace("ref: refs/heads/", string.Empty).Trim();
        }

        return "(detached)";
    }

    public void Stop()
    {
        _headWatcher?.Dispose();
        _headWatcher = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
