using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliSettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliSettingsProvider : ICliSettingsProvider
    {
        // single point of truth for CLI version
        // both, pipeline and download logic are using this value
        public string RequiredDevToolVersion => "f4041f7aa0afe699bb67f01bf296b539fca27cfb"; // 1.0.25
        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";
        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";
        public string CliFileName => $"cs-ide.exe";
        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";
        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);
    }
}
