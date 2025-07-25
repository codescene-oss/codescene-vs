using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Git;
using LibGit2Sharp;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

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
                _logger.Warn("Repository path is null. Aborting retrieval of file content for specific commit.");
                return "";
            }

            var repo = new Repository(repoPath);
            var commitHash = GetBranchCreationCommit(path, repo);

            if (string.IsNullOrEmpty(commitHash)) return "";

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
            _logger.Warn($"Could get file content for specific commit: {e.Message} \n{e.StackTrace.Trim()}");
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
}
