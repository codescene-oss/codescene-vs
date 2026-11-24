// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Extension;

namespace Codescene.VSExtension.Core.Application;

[Export(typeof(IProviderKeyStore))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ProviderKeyStore : IProviderKeyStore
{
    private readonly ISettingsProvider _settings;

    [ImportingConstructor]
    public ProviderKeyStore(ISettingsProvider settings)
    {
        _settings = settings;
    }

    public IDictionary<string, string> GetAll()
    {
        var result = new Dictionary<string, string>();

        foreach (var providerKey in ProviderKeys.All)
        {
            var value = Read(providerKey.Name)?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                result[providerKey.Name] = value;
            }
        }

        return result;
    }

    private string Read(string provider)
    {
        return provider switch
        {
            "anthropic" => _settings.AnthropicApiKey,
            "openai" => _settings.OpenAIApiKey,
            "google" => _settings.GoogleApiKey,
            _ => null,
        };
    }
}
