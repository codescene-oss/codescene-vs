// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Application.Agent
{
    public static class ProviderApiKeyActions
    {
        public const string ConnectivityNotUsableMessage = "Enable \"Enable Refactoring Agent\" and sign in to CodeScene to test provider connectivity.";

        public static IReadOnlyList<ProviderItem> Items(IProviderKeyStore store)
        {
            return ProviderKeys.All.Select(provider => new ProviderItem
            {
                Provider = provider,
                KeyStored = store.IsStored(provider.Name),
            }).ToList();
        }

        public static string Apply(IProviderKeyStore store, string providerName, string value)
        {
            var provider = ProviderKeys.GetByName(providerName);
            if (provider == null)
            {
                return null;
            }

            store.Store(providerName, value);

            var trimmedValue = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedValue))
            {
                return $"Removed the {provider.Label} API key.";
            }

            return $"Stored the {provider.Label} API key securely.";
        }

        public static string ConnectivityReport(ConnectivityResult result)
        {
            if (result == null)
            {
                return null;
            }

            if (result.Cancelled)
            {
                return null;
            }

            if (result.Success)
            {
                return "Provider connectivity test successful.";
            }

            return $"Provider connectivity test failed: {result.Error}";
        }

        public class ProviderItem
        {
            public ProviderKey Provider { get; set; }

            public bool KeyStored { get; set; }
        }
    }
}
