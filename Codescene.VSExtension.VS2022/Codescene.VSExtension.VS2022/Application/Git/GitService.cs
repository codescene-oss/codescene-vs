using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Git;
using LibGit2Sharp;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    [Export(typeof(IGitService))]
    public class GitService : IGitService
    {
        [Import] private readonly ILogger _logger;

        private static readonly HashSet<string> PossibleMainBranches =
            new(StringComparer.OrdinalIgnoreCase) { "main", "master", "develop", "trunk", "dev" };


        public string GetBranchCreationCommit(string repoPathOrFile, string currentBranch)
        {
            try
            {
                using var repo = OpenRepository(repoPathOrFile);
                if (repo is null)
                {
                    _logger.Warn($"No git repository found for path: {repoPathOrFile}");
                    return "";
                }

                if (string.IsNullOrWhiteSpace(currentBranch))
                    return "";

                var branch = repo.Branches[currentBranch];
                if (branch is null)
                {
                    _logger.Warn($"Branch '{currentBranch}' not found in repo {repo.Info.WorkingDirectory ?? repo.Info.Path}");
                    return "";
                }

                if (IsMainBranch(currentBranch))
                    return "";

                var created = TryGetCreatedFromReflog(repo, branch);
                if (!string.IsNullOrEmpty(created))
                    return created;

                // compute fork-point against default branch (merge-base)
                var def = GetDefaultBranchRef(repo);
                if (def?.Tip != null && branch.Tip != null)
                {
                    var fork = repo.ObjectDatabase.FindMergeBase(branch.Tip, def.Tip);
                    if (fork != null)
                        return fork.Sha;
                }

                // common ancestor via divergence
                if (def?.Tip != null && branch.Tip != null)
                {
                    var div = repo.ObjectDatabase.CalculateHistoryDivergence(def.Tip, branch.Tip);
                    var common = div?.CommonAncestor;
                    if (common != null)
                        return common.Sha;
                }

                return "";
            }
            catch (Exception e)
            {
                _logger.Error($"Could not get branch creation commit for '{currentBranch}'", e);
                return "";
            }
        }

        public string GetFileContentForCommit(string path, string commitSha)
        {
            try
            {
                using var repo = OpenRepository(path);
                if (repo is null)
                {
                    _logger.Warn("Repository path is null. Aborting retrieval of file content for specific commit.");
                    return "";
                }

                if (string.IsNullOrWhiteSpace(commitSha))
                {
                    _logger.Warn("Commit SHA is null or empty. Cannot lookup commit.");
                    return "";
                }

                var repoRoot = repo.Info.WorkingDirectory;
                if (string.IsNullOrEmpty(repoRoot))
                {
                    _logger.Warn("Bare repository detected; cannot map workdir relative paths.");
                    return "";
                }

                var relativePath = GetRelativePath(repoRoot, path).Replace("\\", "/");

                var commit = repo.Lookup<Commit>(commitSha);
                if (commit is null)
                {
                    _logger.Warn($"Commit {commitSha} not found in repository {repo.Info.WorkingDirectory ?? repo.Info.Path}");
                    return "";
                }

                var entry = commit[relativePath];
                if (entry is null)
                {
                    _logger.Warn($"File '{relativePath}' not found in commit {commitSha}");
                    return "";
                }

                var blob = (Blob)entry.Target;
                return blob.GetContentText(); // UTF-8
            }
            catch (Exception e)
            {
                _logger.Warn($"Could not get file content for specific commit: {e.Message}\n{e.StackTrace?.Trim()}");
                return "";
            }
        }

        public string GetHeadCommit(string repoPathOrFile)
        {
            using var repo = OpenRepository(repoPathOrFile)
                ?? throw new InvalidOperationException($"No Git repo found for {repoPathOrFile}");

            return repo.Head?.Tip?.Sha ?? "";
        }

        public string GetCurrentBranch(string repoPathOrFile)
        {
            using var repo = OpenRepository(repoPathOrFile)
                ?? throw new InvalidOperationException($"No Git repo found for {repoPathOrFile}");

            return repo.Head?.FriendlyName ?? "";
        }

        public string GetDefaultBranch(string repoPathOrFile)
        {
            using var repo = OpenRepository(repoPathOrFile)
                ?? throw new InvalidOperationException($"No Git repo found for {repoPathOrFile}");

            var def = GetDefaultBranchName(repo);
            return def ?? "";
        }

        public static bool IsMainBranch(string currentBranch)
        {
            if (string.IsNullOrWhiteSpace(currentBranch)) return false;
            return PossibleMainBranches.Contains(currentBranch);
        }

        
        private static Repository? OpenRepository(string pathOrFile)
        {
            if (string.IsNullOrWhiteSpace(pathOrFile)) return null;

            var start = File.Exists(pathOrFile) ? Path.GetDirectoryName(pathOrFile)! : pathOrFile;
            var discovered = Repository.Discover(start);
            if (string.IsNullOrEmpty(discovered)) return null;

            // LibGit2Sharp accepts either the .git dir or the workdir; discovered points to the .git
            return new Repository(discovered);
        }

        /// <summary>
        /// Try to find "created from" in the branch reflog.
        /// </summary>
        private static string TryGetCreatedFromReflog(Repository repo, Branch branch)
        {
            try
            {
                var log = repo.Refs.Log(branch.CanonicalName);

                // Search oldest→newest to catch the creation entry
                var entry = log
                    .Reverse()
                    .FirstOrDefault(e =>
                        e.Message != null &&
                        (e.Message.IndexOf("created from", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         e.Message.StartsWith("branch:", StringComparison.OrdinalIgnoreCase) ||
                         e.Message.StartsWith("checkout:", StringComparison.OrdinalIgnoreCase)));

                return entry?.To?.Sha ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Best-effort default branch (prefers remote origin/HEAD).
        /// </summary>
        private static Branch GetDefaultBranchRef(Repository repo)
        {
            var originHead = repo.Refs["refs/remotes/origin/HEAD"] as SymbolicReference;
            if (originHead?.TargetIdentifier is string targetId)
            {
                // targetId e.g. "refs/remotes/origin/main"
                var localName = targetId.Split('/').Last(); // "main"
                var local = repo.Branches[localName];
                if (local != null) return local;

                // fall back to remote branch if local doesn’t exist
                var remote = repo.Branches[$"origin/{localName}"];
                if (remote != null) return remote;
            }

            // Fall back to well-known names (local first)
            foreach (var name in PossibleMainBranches)
            {
                var b = repo.Branches[name] ?? repo.Branches[$"origin/{name}"];
                if (b != null) return b;
            }

            var upstream = repo.Head?.TrackedBranch;
            if (upstream != null) return upstream;

            return null;
        }

        private static string GetDefaultBranchName(Repository repo)
        {
            var b = GetDefaultBranchRef(repo);
            if (b == null) return null;

            if (!string.IsNullOrEmpty(b.FriendlyName))
            {
                return b.FriendlyName.StartsWith("origin/", StringComparison.OrdinalIgnoreCase)
                    ? b.FriendlyName.Substring("origin/".Length)
                    : b.FriendlyName;
            }
            return b.CanonicalName?.Split('/').Last();
        }

        public static string GetRelativePath(string basePath, string fullPath)
        {
            var baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
            var fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            if (string.IsNullOrEmpty(path)) return Path.DirectorySeparatorChar.ToString();
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }
    }
}
