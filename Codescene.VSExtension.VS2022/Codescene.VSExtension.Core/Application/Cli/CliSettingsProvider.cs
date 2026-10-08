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
        public string RequiredDevToolVersion => "05282e918f9e90f08a572b3142e8b3abac983d1e"; // 1.0.72

        public string RequiredCliBinarySha256 => "02e99fc7460560cb36aba6f39451098d0f93bc30d62ebdc7c1b1a42dcfc1fa70";

        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";

        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";

        public string CliFileName => "cs-ide.exe";

        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";

        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);
    }
}
