using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliSettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliSettingsProvider : ICliSettingsProvider
    {
        public string RequiredDevToolVersion => "6ab865324e2d15f95b1a41477e33f741883f7414";
        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";
        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";
        public string CliFileName => $"cs-ide.exe";
        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";
        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);
    }
}
