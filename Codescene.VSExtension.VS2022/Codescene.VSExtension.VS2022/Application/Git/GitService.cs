using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Git;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    [Export(typeof(IGitService))]
    public partial class GitService : IGitService
    {
        [Import] private readonly ILogger _logger;

        private static readonly BranchName[] DefaultBranches =
        {
            BranchName.From("main"),
            BranchName.From("master"),
            BranchName.From("develop"),
            BranchName.From("trunk"),
            BranchName.From("dev"),
        };

        private CommitSha? GetBranchCreationCommit(RepoPath repoPath)
        {
            try
            {
                using var repo = OpenRepository(repoPath);
                if (repo is null)
                {
                    Log.Warn(_logger, LogEvent.NoRepository(repoPath));
                    return null;
                }

                var currentBranchName = GetCurrentBranch(repoPath);
                if (currentBranchName.IsEmpty) return null;

                // If on a main/default branch -> no creation SHA.
                if (DefaultBranchesAnyMatch(currentBranchName)) return null;

                var branch = ResolveBranch(repo, currentBranchName);
                if (branch is null) return null;

                return ResolveCreationCommit(repo, branch);
            }
            catch (Exception e)
            {
                Log.Error(_logger, LogEvent.CreationCommitFailed(repoPath), e);
                return null;
            }
        }

        private string GetFileContentForCommit(FileAtCommit request)
        {
            try
            {
                using var repo = OpenRepository(RepoPath.From(request.File.Value));
                if (repo is null)
                {
                    Log.Warn(_logger, LogEvent.NoRepository(RepoPath.From(request.File.Value)));
                    return "";
                }

                var repoRoot = repo.Info.WorkingDirectory;
                if (string.IsNullOrEmpty(repoRoot))
                {
                    Log.Warn(_logger, LogEvent.BareRepositoryDetected());
                    return "";
                }

                var baseUri = new Uri(FsPath.From(repoRoot).EnsureTrailingSeparator().Value);
                var fullUri = new Uri(request.File.Value);
                var rel = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString()).Replace('\\', '/');

                var commit = repo.Lookup<Commit>(request.Sha.Value);
                if (commit is null)
                {
                    Log.Warn(_logger, LogEvent.CommitNotFound(request.Sha, FsPath.From(repo.Info.WorkingDirectory ?? repo.Info.Path)));
                    return "";
                }

                var entry = commit[rel];
                if (entry is null)
                {
                    Log.Warn(_logger, LogEvent.FileNotFoundInCommit(rel, request.Sha));
                    return "";
                }

                var blob = (Blob)entry.Target;
                return blob.GetContentText(); // libgit2sharp returns UTF-8 text
            }
            catch (Exception e)
            {
                Log.Warn(_logger, LogEvent.FileContentLookupFailed(e));
                return "";
            }
        }

        private CommitSha? GetHeadCommit(RepoPath repoPath)
        {
            using var repo = OpenRepository(repoPath);
            return CommitSha.CreateOrNull(repo?.Head?.Tip?.Sha);
        }

        private BranchName GetCurrentBranch(RepoPath repoPath)
        {
            using var repo = OpenRepository(repoPath);
            return BranchName.From(repo?.Head?.FriendlyName);
        }

        private BranchName GetDefaultBranch(RepoPath repoPath)
        {
            using var repo = OpenRepository(repoPath);
            if (repo is null) return BranchName.Empty;

            var b = GetDefaultBranchRef(repo);
            if (b is null) return BranchName.Empty;

            if (!string.IsNullOrEmpty(b.FriendlyName))
            {
                var fn = b.FriendlyName;
                const string originPrefix = "origin/";
                if (fn.StartsWith(originPrefix, StringComparison.OrdinalIgnoreCase))
                    return BranchName.From(fn.Substring(originPrefix.Length));
                return BranchName.From(fn);
            }

            var last = b.CanonicalName?.Split('/').LastOrDefault();
            return BranchName.From(last);
        }


        // ====== Repo + Branch utilities ======

        private static Repository OpenRepository(RepoPath path)
        {
            var start = File.Exists(path.Value)
                ? Path.GetDirectoryName(path.Value) ?? path.Value
                : path.Value;

            var discovered = Repository.Discover(start);
            return string.IsNullOrEmpty(discovered) ? null : new Repository(discovered);
        }

        private static Branch ResolveBranch(Repository repo, BranchName name)
        {
            if (name.IsEmpty) return null;

            return repo.Branches[name.Value] ?? repo.Branches[$"origin/{name.Value}"];
        }

        /// <summary>
        /// Resolve "creation" commit for a branch: reflog → fork-point vs default → common ancestor.
        /// </summary>
        private static CommitSha? ResolveCreationCommit(Repository repo, Branch branch)
        {
            return TryCreatedFromReflog(repo, branch)
                ?? TryForkPoint(repo, branch)
                ?? TryCommonAncestor(repo, branch);
        }

        private static CommitSha? TryCreatedFromReflog(Repository repo, Branch branch)
        {
            try
            {
                var log = repo.Refs.Log(branch.CanonicalName);
                var entry = log
                    .Reverse()
                    .FirstOrDefault(e =>
                        e.Message != null &&
                       (e.Message.IndexOf("created from", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        e.Message.StartsWith("branch:", StringComparison.OrdinalIgnoreCase) ||
                        e.Message.StartsWith("checkout:", StringComparison.OrdinalIgnoreCase)));

                return CommitSha.CreateOrNull(entry?.To?.Sha);
            }
            catch
            {
                return null;
            }
        }

        private static CommitSha? TryForkPoint(Repository repo, Branch branch)
        {
            var def = GetDefaultBranchRef(repo);
            var defTip = def?.Tip;
            var brTip = branch.Tip;
            if (defTip is null || brTip is null) return null;

            var fork = repo.ObjectDatabase.FindMergeBase(brTip, defTip);
            return CommitSha.CreateOrNull(fork?.Sha);
        }

        private static CommitSha? TryCommonAncestor(Repository repo, Branch branch)
        {
            var def = GetDefaultBranchRef(repo);
            var defTip = def?.Tip;
            var brTip = branch.Tip;
            if (defTip is null || brTip is null) return null;

            var div = repo.ObjectDatabase.CalculateHistoryDivergence(defTip, brTip);
            return CommitSha.CreateOrNull(div?.CommonAncestor?.Sha);
        }

        /// <summary>
        /// Best-effort default branch (prefers remote origin/HEAD, then known names, then tracked branch).
        /// </summary>
        private static Branch GetDefaultBranchRef(Repository repo)
        {
            var fromOriginHead = TryResolveOriginHead(repo);
            if (fromOriginHead != null) return fromOriginHead;

            var fromKnown = FindFirstExistingBranch(repo, DefaultBranches);
            if (fromKnown != null) return fromKnown;

            return repo.Head?.TrackedBranch;
        }

        private static Branch TryResolveOriginHead(Repository repo)
        {
            var sym = repo.Refs["refs/remotes/origin/HEAD"] as SymbolicReference;
            if (sym?.TargetIdentifier is not string targetId) return null;

            // targetId e.g. "refs/remotes/origin/main" → "main"
            var name = BranchName.From(targetId.Split('/').LastOrDefault());
            return ResolveBranch(repo, name);
        }

        private static Branch FindFirstExistingBranch(Repository repo, IEnumerable<BranchName> names)
        {
            foreach (var n in names)
            {
                var b = ResolveBranch(repo, n);
                if (b != null) return b;
            }
            return null;
        }

        private static bool DefaultBranchesAnyMatch(BranchName candidate)
        {
            if (candidate.IsEmpty) return false;
            for (int i = 0; i < DefaultBranches.Length; i++)
            {
                if (candidate.Equals(DefaultBranches[i])) return true;
            }
            return false;
        }


        // ====== Typed Logging to avoid string-heavy callsites ======
        private static class Log
        {
            public static void Warn(ILogger logger, LogEvent evt) => logger.Warn(evt.Format());
            public static void Error(ILogger logger, LogEvent evt, Exception ex) => logger.Error(evt.Format(), ex);
        }

        private readonly struct LogEvent
        {
            private readonly string _message;
            private LogEvent(string message) => _message = message;
            public string Format() => _message;

            public static LogEvent NoRepository(RepoPath path) =>
                new LogEvent($"No git repository found for path: {path.Value}");

            public static LogEvent BareRepositoryDetected() =>
                new LogEvent("Bare repository detected; cannot map workdir relative paths.");

            public static LogEvent CommitNotFound(CommitSha sha, FsPath repoPath) =>
                new LogEvent($"Commit {sha.Value} not found in repository {repoPath.Value}");

            public static LogEvent FileNotFoundInCommit(string relPath, CommitSha sha) =>
                new LogEvent($"File '{relPath}' not found in commit {sha.Value}");

            public static LogEvent FileContentLookupFailed(Exception e) =>
                new LogEvent($"Could not get file content for specific commit: {e.Message}\n{e.StackTrace?.Trim()}");

            public static LogEvent CreationCommitFailed(RepoPath repo) =>
                new LogEvent($"Could not get branch creation commit for repository '{repo.Value}'");
        }


        // ====== Domain Value Objects ======

        private readonly struct RepoPath
        {
            public string Value { get; }
            private RepoPath(string value) => Value = value;
            public static bool TryCreate(string s, out RepoPath path)
            {
                if (!string.IsNullOrWhiteSpace(s)) { path = new RepoPath(s); return true; }
                path = default; return false;
            }
            public static RepoPath From(string s) => new RepoPath(s ?? "");
            public override string ToString() => Value;
        }

        private readonly struct AbsolutePath
        {
            public string Value { get; }
            private AbsolutePath(string value) => Value = value;
            public static bool TryCreate(string s, out AbsolutePath path)
            {
                if (!string.IsNullOrWhiteSpace(s)) { path = new AbsolutePath(s); return true; }
                path = default; return false;
            }
            public override string ToString() => Value;
        }

        private readonly struct FsPath
        {
            public string Value { get; }
            private FsPath(string value) => Value = value;
            public static FsPath From(string s) => new FsPath(s ?? "");
            public FsPath EnsureTrailingSeparator()
            {
                if (string.IsNullOrEmpty(Value)) return new FsPath(Path.DirectorySeparatorChar.ToString());
                var sep = Path.DirectorySeparatorChar.ToString();
                return Value.EndsWith(sep, StringComparison.Ordinal) ? this : new FsPath(Value + sep);
            }
            public override string ToString() => Value;
        }

        private readonly struct FileAtCommit
        {
            public AbsolutePath File { get; }
            public CommitSha Sha { get; }
            public FileAtCommit(AbsolutePath file, CommitSha sha)
            {
                File = file;
                Sha = sha;
            }
        }

        private readonly struct BranchName : IEquatable<BranchName>
        {
            public string Value { get; }
            public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
            private BranchName(string value) => Value = value ?? "";

            public static BranchName From(string s) => new BranchName(s ?? "");
            public static BranchName Empty => new BranchName("");

            public bool Equals(BranchName other) =>
                string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is BranchName other && Equals(other);
            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? "");
            public override string ToString() => Value;
        }

        private readonly struct CommitSha
        {
            public string Value { get; }
            private CommitSha(string value) => Value = value;

            public static bool TryCreate(string s, out CommitSha sha)
            {
                if (!string.IsNullOrWhiteSpace(s)) { sha = new CommitSha(s); return true; }
                sha = default; return false;
            }

            public static CommitSha? CreateOrNull(string s)
                => string.IsNullOrWhiteSpace(s) ? (CommitSha?)null : new CommitSha(s);

            public override string ToString() => Value;
        }
    }
}
