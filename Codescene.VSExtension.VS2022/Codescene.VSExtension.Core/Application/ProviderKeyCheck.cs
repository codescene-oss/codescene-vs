// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Application;

public static class ProviderKeyCheck
{
    private static readonly Dictionary<string, string[]> ProviderEnvVars = new Dictionary<string, string[]>()
    {
        { "anthropic", new[] { "ANTHROPIC_API_KEY" } },
        { "openai", new[] { "OPENAI_API_KEY" } },
        { "google", new[] { "GOOGLE_API_KEY", "GOOGLE_GENERATIVE_AI_API_KEY", "GEMINI_API_KEY" } },
    };

    public static string FindMissing(string model, IReadOnlyCollection<string> storedProviders, Func<string, string> env)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var slashIndex = model.IndexOf('/');
        if (slashIndex <= 0)
        {
            return null;
        }

        var provider = model.Substring(0, slashIndex);

        if (!ProviderEnvVars.ContainsKey(provider))
        {
            return null;
        }

        if (storedProviders.Contains(provider))
        {
            return null;
        }

        var envVars = ProviderEnvVars[provider];
        if (envVars.Any(varName => !string.IsNullOrWhiteSpace(env(varName))))
        {
            return null;
        }

        return provider;
    }

    public static string MissingMessage(string provider, string model)
    {
        var providerKey = ProviderKeys.GetByName(provider);
        var label = providerKey?.Label ?? provider;
        return $"No {label} API key is set for {model}. Set one in Tools > Options > CodeScene > General > Refactoring Agent.";
    }
}
