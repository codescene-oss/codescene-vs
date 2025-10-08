using Codescene.VSExtension.Core.Application.Services.Cache.Review;
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

    private static readonly string[] PossibleMainBranches = ["main", "master", "develop", "trunk", "dev"];


    public string GetBranchCreationCommit(string repoPathOrFile, string currentBranch)
    {
        try
        {
            var repoPath = Repository.Discover(repoPathOrFile);
            if (string.IsNullOrEmpty(repoPath))
            {
                _logger.Warn($"No git repository found for path: {repoPathOrFile}");
                return "";
            }
            var repo = new Repository(repoPath);
            var branch = repo.Branches[currentBranch];

            if (branch == null || repoPath == null)
            {
                _logger.Warn($"Branch {currentBranch} not found in repo {repoPath}");
                return "";
            }

            if (IsMainBranch(currentBranch)) return "";


            var reflog = repo.Refs.Log(branch.CanonicalName);

            var creationEntry = reflog
                .Reverse() // check from oldest to newest
                .FirstOrDefault(entry =>
                    entry.Message != null &&
                    entry.Message.IndexOf("created from", StringComparison.OrdinalIgnoreCase) >= 0);

            if (creationEntry != null)
            {
                _logger.Debug($"Branch {currentBranch} creation found in reflog: {creationEntry.To.Sha}");
                return creationEntry.To.Sha;
            }
            return "";
        }
        catch (Exception e)
        {
            _logger.Error($"Could not determine branch creation commit for {currentBranch}", e);
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

            if (string.IsNullOrEmpty(commitSha))
            {
                _logger.Warn("Commit SHA is null or empty. Cannot lookup commit.");
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
        repoPath = NormalizeRepoPath(repoPath);
        if (repoPath == null)
            throw new InvalidOperationException($"No Git repo found for {repoPath}");

        using var repo = new Repository(repoPath);
        return repo.Branches["main"]?.FriendlyName
            ?? repo.Branches["master"]?.FriendlyName
            ?? repo.Branches["develop"]?.FriendlyName
            ?? repo.Branches["trunk"]?.FriendlyName
            ?? repo.Branches["dev"]?.FriendlyName
            ?? "";
    }

    public static bool IsMainBranch(string currentBranch)
    {
        if (string.IsNullOrEmpty(currentBranch))
            return false;

        return PossibleMainBranches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase);
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
