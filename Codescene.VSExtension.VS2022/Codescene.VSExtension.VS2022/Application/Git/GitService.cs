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

    public string GetBranchCreationCommit(string repoPath, string branchName)
    {
        try
        {
            using var repo = new Repository(repoPath);

            var branch = repo.Branches[branchName];
            if (branch == null)
            {
                _logger.Warn($"Branch {branchName} not found in repo {repoPath}");
                return null;
            }

            // 1️⃣ Try reflog approach
            try
            {
                var reflog = repo.Refs.Log(branch.CanonicalName);

                var creationEntry = reflog
                    .Reverse() // check from oldest to newest
                    .FirstOrDefault(entry =>
                        entry.Message != null &&
                        entry.Message.IndexOf("created from", StringComparison.OrdinalIgnoreCase) >= 0);

                if (creationEntry != null)
                {
                    _logger.Debug($"Branch {branchName} creation found in reflog: {creationEntry.To.Sha}");
                    return creationEntry.To.Sha;
                }
            }
            catch (Exception e)
            {
                _logger.Warn($"Reflog lookup failed for branch {branchName}: {e.Message}");
            }

            // 2️⃣ Fallback: commit graph approach
            var firstCommit = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = branch })
                                 .LastOrDefault();

            if (firstCommit != null)
            {
                _logger.Debug($"Branch {branchName} creation fallback (commit graph): {firstCommit.Sha}");
                return firstCommit.Sha;
            }

            return null;
        }
        catch (Exception e)
        {
            _logger.Error($"Could not determine branch creation commit for {branchName}", e);
            return null;
        }
    }


    public string GetFileContentForCommit(string path, string commitSha)
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

            var repoRoot = repo.Info.WorkingDirectory;
            var relativePath = GetRelativePath(repoRoot, path).Replace("\\", "/");

            var commit = repo.Lookup<Commit>(commitSha);
            if (commit == null)
            {
                _logger.Warn($"Commit {commitSha} not found in repository {repoPath}");
                return "";
            }

            var entry = commit[relativePath];
            if (entry == null)
            {
                _logger.Warn($"File {relativePath} not found in commit {commitSha}");
                return "";
            }

            var blob = (Blob)entry.Target;
            return blob.GetContentText(); // Gets the content as UTF-8 string
        }
        catch (Exception e)
        {
            _logger.Warn($"Could get file content for specific commit: {e.Message} \n{e.StackTrace.Trim()}");
            return "";
        }
    }

    public string GetHeadCommit(string repoPathOrFile)
    {
        var repoPath = NormalizeRepoPath(repoPathOrFile);
        if (repoPath == null)
            throw new InvalidOperationException($"No Git repo found for {repoPathOrFile}");

        using var repo = new Repository(repoPath);
        return repo.Head.Tip.Sha;
    }

    public string GetCurrentBranch(string repoPathOrFile)
    {
        var repoPath = NormalizeRepoPath(repoPathOrFile);
        if (repoPath == null)
            throw new InvalidOperationException($"No Git repo found for {repoPathOrFile}");

        using var repo = new Repository(repoPath);
        return repo.Head.FriendlyName;
    }


    public string GetDefaultBranch(string repoPath)
    {
        using var repo = new Repository(repoPath);
        return repo.Branches["main"]?.FriendlyName
            ?? repo.Branches["master"]?.FriendlyName
            ?? repo.Branches["develop"]?.FriendlyName
            ?? repo.Branches["trunk"]?.FriendlyName
            ?? repo.Branches["dev"]?.FriendlyName
            ?? "main"; // fallback
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

    private string FindRepoPathFromFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        return Repository.Discover(dir); // walks up until it finds .git
    }

    private string NormalizeRepoPath(string repoPathOrFile)
    {
        if (string.IsNullOrEmpty(repoPathOrFile))
            return null;

        // If it's a file → use its parent directory
        var dir = File.Exists(repoPathOrFile)
            ? Path.GetDirectoryName(repoPathOrFile)
            : repoPathOrFile;

        // Walk upwards until a .git is found
        return Repository.Discover(dir);
    }

}
