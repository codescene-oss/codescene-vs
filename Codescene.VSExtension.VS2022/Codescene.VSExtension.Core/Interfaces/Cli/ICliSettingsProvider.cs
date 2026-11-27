// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliSettingsProvider
    {
        string RequiredDevToolVersion { get; }

        string RequiredCliBinarySha256 { get; }

        string ArtifactBaseUrl { get; }

        string CliArtifactName { get; }

        string CliArtifactUrl { get; }

        string CliFileName { get; }

        string CliFileFullPath { get; }

        string RequiredAgentVersion { get; }

        string RequiredAgentBinarySha256 { get; }

        string AgentFileName { get; }

        string AgentFileFullPath { get; }
    }
}
