namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliCommandProvider
    {
        string VersionCommand { get; }
        string DeviceIdCommand { get; }
        string GetReviewPathCommand(string path);
        string SendTelemetryCommand(string jsonEvent);
        string GetReviewFileContentCommand(string path);
    }
}
