// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliSettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliSettingsProvider : ICliSettingsProvider
    {
        public string RequiredDevToolVersion => "21ac9263043b0bca2f216edbd61255f54edc77b3"; // 1.0.50

        public string DistributionDirectoryName => "cs-windows-amd64";

        public string CliArtifactName => $"cs-ide-jre-windows-amd64-{RequiredDevToolVersion}.zip";

        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";

        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";

        public string DistributionFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), DistributionDirectoryName);

        public string JavaExeFullPath => Path.Combine(DistributionFullPath, "jre", "bin", "java.exe");

        public string JarFullPath => Path.Combine(DistributionFullPath, "cs-ide.jar");
    }
}
