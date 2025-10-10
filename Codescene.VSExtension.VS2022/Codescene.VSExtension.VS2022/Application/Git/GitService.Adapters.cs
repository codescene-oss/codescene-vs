using Codescene.VSExtension.Core.Application.Services.Git;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    public partial class GitService : IGitService
    {
        public string GetBranchCreationCommit(string repoPathOrFile)
        {
            if (!RepoPath.TryCreate(repoPathOrFile, out var repo)) return "";
            var sha = GetBranchCreationCommit(repo);
            return sha.HasValue ? sha.Value.Value : "";
        }

        public string GetFileContentForCommit(string path, string commitSha)
        {
            if (!AbsolutePath.TryCreate(path, out var file)) return "";
            if (!CommitSha.TryCreate(commitSha, out var sha)) return "";
            return GetFileContentForCommit(new FileAtCommit(file, sha));
        }

        public string GetHeadCommit(string repoPathOrFile)
        {
            if (!RepoPath.TryCreate(repoPathOrFile, out var repo)) return "";
            var head = GetHeadCommit(repo);
            return head.HasValue ? head.Value.Value : "";
        }

        public string GetCurrentBranch(string repoPathOrFile)
        {
            if (!RepoPath.TryCreate(repoPathOrFile, out var repo)) return "";
            var name = GetCurrentBranch(repo);
            return name.IsEmpty ? "" : name.Value;
        }

        public string GetDefaultBranch(string repoPathOrFile)
        {
            if (!RepoPath.TryCreate(repoPathOrFile, out var repo)) return "";
            var name = GetDefaultBranch(repo);
            return name.IsEmpty ? "" : name.Value;
        }
    }
}
