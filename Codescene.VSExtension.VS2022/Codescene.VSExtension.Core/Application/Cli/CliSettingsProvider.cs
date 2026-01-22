using Codescene.VSExtension.Core.Interfaces.Cli;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliSettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliSettingsProvider : ICliSettingsProvider
    {
        // single point of truth for CLI version
        // both, pipeline and download logic are using this value
        public string RequiredDevToolVersion => "b98bdcaf4ac46597a73113d6fca6635d3f3393a5"; // 1.0.26
        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";
        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";
        public string CliFileName => $"cs-ide.exe";
        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";
        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);
    }
}
