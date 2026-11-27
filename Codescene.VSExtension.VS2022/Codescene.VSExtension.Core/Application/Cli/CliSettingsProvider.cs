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
        public string RequiredDevToolVersion => "8be3c200a676419baf2892b5fb6cb0b77eb26d1a"; // 1.0.101

        public string RequiredCliBinarySha256 => "d8ae7566bfeacfc78fe8dc1e577f1f92d10b1db24d08872353d8d96a19fbb092";

        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";

        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";

        public string CliFileName => "cs-ide.exe";

        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";

        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);

        public string RequiredAgentVersion => "1.1.13";

        public string RequiredAgentBinarySha256 => "02baa47e48b044a89f936b897e57b63430d45f0dcdfd83fb87485d35df33e855";

        public string AgentFileName => "cs-agent.exe";

        public string AgentFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), AgentFileName);
    }
}
