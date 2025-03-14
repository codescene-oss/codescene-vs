namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliCommandProvider
    {
        string VersionCommand { get; }
        string GetReviewPathCommand(string path);
        string GetReviewFileContentCommand(string path);
    }
}
