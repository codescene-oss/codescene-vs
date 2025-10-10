using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliCommandProvider
    {
        string VersionCommand { get; }
        string DeviceIdCommand { get; }
        string GetReviewPathCommand(string path);
        string SendTelemetryCommand(string jsonEvent);
        string GetReviewFileContentCommand(string path);
        string GetReviewDeltaCommand(string oldScore, string newScore);
        string GetRefactorCommandWithCodeSmells(string extension, string codeSmells, string preflight = null);
        string GetRefactorCommandWithDeltaResult(string extension, string deltaResult, string preflight = null);
        string GetPreflightSupportInformationCommand(bool force);
        string GetRefactorPostCommand(FnToRefactorModel fnToRefactor, bool skipCache, string token = null);
    }
}
