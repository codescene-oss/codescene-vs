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
        public string RequiredDevToolVersion => "683aae4993737973b2309118d42c85acc444c7cd"; // 1.0.98

        public string RequiredCliBinarySha256 => "2bfcf9d772536359da5711ce6727ae73f74c7e474fb81ac32f759d4758227e53";

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
