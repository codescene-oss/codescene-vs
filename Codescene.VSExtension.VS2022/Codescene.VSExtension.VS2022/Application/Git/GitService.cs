using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Git;
using LibGit2Sharp;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Codescene.VSExtension.VS2022.Application.Git;

[Export(typeof(IGitService))]
public class GitService : IGitService
{
    [Import]
    private readonly ILogger _logger;

    public string GetBranchCreationCommit(string path, Repository repository)
    {
        try
        {
            var currentBranch = repository.Head;

            var branch = repository.Head;
            var reflog = repository.Refs.Log(branch.CanonicalName);

            var creationEntry = reflog
                .Reverse() // latest to oldest
                .FirstOrDefault(entry =>
                    entry.Message != null &&
                    entry.Message.IndexOf("created from", StringComparison.OrdinalIgnoreCase) >= 0);

            if (creationEntry != null)
            {
                return creationEntry.To.Sha;
            }

            return ""; // Possibly created directly on main or unknown base
        }
        catch (Exception e)
        {
            _logger.Error("Could not get branch creation commit.", e);
            return "";
        }
    }

    public string GetFileContentForCommit(string path)
    {
        try
        {
            var repoPath = Repository.Discover(path);
            if (string.IsNullOrEmpty(repoPath))
            {
                _logger.Warn("Repository path is null");
                return "";
            }

            var repo = new Repository(repoPath);
            var commitHash = GetBranchCreationCommit(path, repo);

            var repoRoot = repo.Info.WorkingDirectory;
            var relativePath = GetRelativePath(repoRoot, path).Replace("\\", "/");

            var commit = repo.Lookup<Commit>(commitHash);
            var entry = commit[relativePath];

            if (entry == null) return "";

            var blob = (Blob)entry.Target;

            return blob.GetContentText(); // Gets the content as UTF-8 string
        }
        catch (Exception e)
        {
            _logger.Error("Could not do git stuff", e);
            return "";
        }
    }

    // TODO: Move to helper
    public static string GetRelativePath(string basePath, string fullPath)
    {
        var baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
        var fullUri = new Uri(fullPath);
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    // TODO: Move to helper
    private static string AppendDirectorySeparatorChar(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            return path + Path.DirectorySeparatorChar;
        return path;
    }

    private GitResult ExecuteCommand(string path, string arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = Repository.Discover(path),
                RedirectStandardOutput = true, // Captures the output that the process writes to the console (stdout)
                RedirectStandardError = true, // Captures error messages that the process writes to the error stream (stderr)
                UseShellExecute = false, // Runs the process directly without using the shell
                CreateNoWindow = true, // Prevents a new console window from appearing when the process runs
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Capture standard output asynchronously
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };

            // Capture standard error asynchronously
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new GitResult
            {
                ExitCode = process.ExitCode,
                Output = outputBuilder.ToString().Trim(),
                Error = errorBuilder.ToString().Trim()
            };
        }
        catch (Exception ex)
        {
            return new GitResult
            {
                ExitCode = -1,
                Output = string.Empty,
                Error = $"Exception while executing git command: {ex.Message}"
            };
        }
    }
}
