namespace Codescene.VSExtension.Core.Application.Services.Git
{
    public interface IGitService
    {
        string GetFileContentForCommit(string path, string commitSha);
        string GetHeadCommit(string repoPath);
        string GetBranchCreationCommit(string repoPath, string branchName);
        string GetCurrentBranch(string repoPath);
        string GetDefaultBranch(string repoPath);

    }

    public class GitResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Success => ExitCode == 0;
    }
}
