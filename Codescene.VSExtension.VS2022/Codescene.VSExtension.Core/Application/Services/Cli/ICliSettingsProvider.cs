﻿namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliSettingsProvider
    {
        string RequiredDevToolVersion { get; }
        string ArtifactBaseUrl { get; }
        string CliArtifactName { get; }
        string CliArtifactUrl { get; }
        string CliFileName { get; }
        string CliFileFullPath { get; }
    }
}
