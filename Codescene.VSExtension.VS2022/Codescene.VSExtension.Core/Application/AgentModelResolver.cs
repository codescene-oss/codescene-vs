// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces.Extension;

namespace Codescene.VSExtension.Core.Application;

public static class AgentModelResolver
{
    public static string Resolve(ISettingsProvider settings, IReadOnlyCollection<string> storedProviders)
    {
        var modelOverride = settings.AgentModelOverride?.Trim();
        if (!string.IsNullOrWhiteSpace(modelOverride))
        {
            return modelOverride;
        }

        var agentModel = settings.AgentModel?.Trim();
        if (!string.IsNullOrWhiteSpace(agentModel) && agentModel != AgentModels.Default)
        {
            return agentModel;
        }

        var inferredModel = InferFromStoredKeys(storedProviders);
        if (inferredModel != null)
        {
            return inferredModel;
        }

        if (HasBedrockConfiguration(settings))
        {
            return "amazon-bedrock/eu.anthropic.claude-sonnet-5-5";
        }

        return AgentModels.Default;
    }

    private static string InferFromStoredKeys(IReadOnlyCollection<string> storedProviders)
    {
        if (storedProviders.Contains("anthropic"))
        {
            return "anthropic/claude-sonnet-5-5";
        }

        if (storedProviders.Contains("openai"))
        {
            return "openai/gpt-6.1-sol";
        }

        if (storedProviders.Contains("google"))
        {
            return "google/gemini-4-carbon";
        }

        return null;
    }

    private static bool HasBedrockConfiguration(ISettingsProvider settings)
    {
        var bedrockProfile = settings.BedrockProfile?.Trim();
        var bedrockRegion = settings.BedrockRegion?.Trim();
        return !string.IsNullOrWhiteSpace(bedrockProfile) || !string.IsNullOrWhiteSpace(bedrockRegion);
    }
}
