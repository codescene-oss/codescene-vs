// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Codescene.VSExtension.Core.Application;

public class ProviderKey
{
    public ProviderKey(string name, string label, string configField)
    {
        Name = name;
        Label = label;
        ConfigField = configField;
    }

    public string Name { get; }

    public string Label { get; }

    public string ConfigField { get; }
}

public static class ProviderKeys
{
    private static readonly ProviderKey[] _providers =
    [
        new ProviderKey("anthropic", "Anthropic", "anthropic_api_key"),
        new ProviderKey("openai", "OpenAI", "openai_api_key"),
        new ProviderKey("google", "Google (Gemini)", "google_api_key")
    ];

    public static ReadOnlyCollection<ProviderKey> All { get; } = new ReadOnlyCollection<ProviderKey>(_providers);

    public static ProviderKey GetByName(string name)
    {
        return All.FirstOrDefault(p => p.Name == name);
    }
}
