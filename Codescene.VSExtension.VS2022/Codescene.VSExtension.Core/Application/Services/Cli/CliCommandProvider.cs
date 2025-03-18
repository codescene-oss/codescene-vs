namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public class CliCommandProvider : ICliCommandProvider
    {
        public string VersionCommand => "version --sha";
        public string GetReviewFileContentCommand(string path) => $"review --file-name {path}";
        public string GetReviewPathCommand(string path) => $"review {path}";
    }
}
