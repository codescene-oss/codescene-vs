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
        public string RequiredDevToolVersion => "66beda3b9e26e74eacd78f68247b2591196c999d"; // 1.0.44

        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";

        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";

        public string CliFileName => "cs-ide.exe";

        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";

        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);
    }
}
