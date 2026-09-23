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
        // single point of truth for CLI version
        // used by the build pipeline to bundle the CLI with the extension
        public string RequiredDevToolVersion => "3c5dc7a5273e66de39db29c8560d2d7f28f2e09b"; // 1.0.63

        public string RequiredCliBinarySha256 => "f70d090e786d86d326a90a6434a84879922cb6f0fd2eaa6f16019d62deaae935";

        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";

        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";

        public string CliFileName => "cs-ide.exe";

        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";

        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);
    }
}
