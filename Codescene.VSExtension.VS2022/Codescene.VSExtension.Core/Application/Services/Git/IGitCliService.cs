namespace Codescene.VSExtension.Core.Application.Services.Git
{
    public interface IGitService
    {
        GitResult ExecuteGitCommand(string arguments);
    }

    public class GitResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Success => ExitCode == 0;
    }
}
