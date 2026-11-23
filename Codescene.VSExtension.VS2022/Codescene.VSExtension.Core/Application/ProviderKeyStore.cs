// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Security;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application;

[Export(typeof(IProviderKeyStore))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ProviderKeyStore : IProviderKeyStore
{
    private readonly IProviderKeyBackingStore _backingStore;
    private readonly ILogger _logger;

    [ImportingConstructor]
    public ProviderKeyStore(IProviderKeyBackingStore backingStore, ILogger logger)
    {
        _backingStore = backingStore;
        _logger = logger;
    }

    public bool IsStored(string provider)
    {
        var providerKey = ProviderKeys.GetByName(provider);
        if (providerKey == null)
        {
            return false;
        }

        var blob = _backingStore.Get(providerKey.StoreKey);
        if (string.IsNullOrWhiteSpace(blob))
        {
            return false;
        }

        if (ProviderKeyProtector.TryUnprotect(blob, out _))
        {
            return true;
        }

        _logger?.Warn($"Provider key for {provider} could not be decrypted.");
        return false;
    }

    public string Get(string provider)
    {
        var providerKey = ProviderKeys.GetByName(provider);
        if (providerKey == null)
        {
            return null;
        }

        var blob = _backingStore.Get(providerKey.StoreKey);
        if (string.IsNullOrWhiteSpace(blob))
        {
            return null;
        }

        if (ProviderKeyProtector.TryUnprotect(blob, out var plaintext))
        {
            return plaintext;
        }

        _logger?.Warn($"Provider key for {provider} could not be decrypted.");
        return null;
    }

    public IDictionary<string, string> GetAll()
    {
        var result = new Dictionary<string, string>();

        foreach (var providerKey in ProviderKeys.All)
        {
            var value = Get(providerKey.Name);
            if (!string.IsNullOrEmpty(value))
            {
                result[providerKey.Name] = value;
            }
        }

        return result;
    }

    public void Store(string provider, string value)
    {
        var providerKey = ProviderKeys.GetByName(provider);
        if (providerKey == null)
        {
            return;
        }

        var trimmedValue = value?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            _backingStore.Delete(providerKey.StoreKey);
        }
        else
        {
            var protectedValue = ProviderKeyProtector.Protect(trimmedValue);
            _backingStore.Set(providerKey.StoreKey, protectedValue);
        }
    }
}
