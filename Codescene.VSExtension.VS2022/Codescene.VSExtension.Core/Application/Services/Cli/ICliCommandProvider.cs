namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliCommandProvider
    {
        string VersionCommand { get; }
        string GetReviewPathCommand(string path);
        string GetReviewFileContentCommand(string path);
        string GetRefactorCommandWithCodeSmells(string extension, string codeSmellsJson, string preflight = null);
        string GetPreflightSupportInformationCommand(bool force);
    }
}
