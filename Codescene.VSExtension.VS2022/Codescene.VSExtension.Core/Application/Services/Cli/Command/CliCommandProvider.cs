using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliCommandProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliCommandProvider : ICliCommandProvider
    {
        public string VersionCommand => "version --sha";

        public string DeviceIdCommand => "telemetry --device-id";

        public string GetReviewFileContentCommand(string path) => $"review --file-name {path}";

        public string GetReviewPathCommand(string path) => $"review {path}";
    }
}
