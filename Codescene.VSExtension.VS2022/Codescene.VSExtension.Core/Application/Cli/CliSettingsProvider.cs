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
        public string RequiredDevToolVersion => "6a8e86c814724b2d8719909bc96e5eb36b1d7a6c"; // 1.0.76

        public string RequiredCliBinarySha256 => "745d62c1428b8d3715d50b02edbd7a0c5f24381e3bfcfeaacde42e9949973bb5";

        public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";

        public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";

        public string CliFileName => "cs-ide.exe";

        public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";

        public string CliFileFullPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CliFileName);
    }
}
