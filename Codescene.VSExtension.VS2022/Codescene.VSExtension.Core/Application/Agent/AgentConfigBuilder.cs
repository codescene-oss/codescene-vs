// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Agent;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal static class AgentConfigBuilder
    {
        public static string Build(
            string token,
            AgentSkill skill,
            string ioDir,
            AuthSnapshot snapshot,
            IReadOnlyDictionary<string, string> keys,
            ISettingsProvider settings)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (skill == null)
            {
                throw new ArgumentNullException(nameof(skill));
            }

            var config = new JObject
            {
                ["codescene_access_token"] = token,
                ["plugins"] = new JArray(skill.Plugin),
                ["tracking"] = new JObject
                {
                    ["environment"] = "codescene-vs",
                },
            };

            AddOnPremUrl(config, snapshot);
            AddIoDir(config, ioDir);
            AddProviderKeys(config, keys);
            AddBedrock(config, settings);

            return config.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static void AddOnPremUrl(JObject config, AuthSnapshot snapshot)
        {
            var server = snapshot?.Status?["server"];
            var onprem = server?["onprem"]?.Value<bool>() ?? false;
            var url = server?["url"]?.ToString();

            if (onprem && !string.IsNullOrEmpty(url))
            {
                config["codescene_onprem_url"] = url;
            }
        }

        private static void AddIoDir(JObject config, string ioDir)
        {
            if (!string.IsNullOrEmpty(ioDir))
            {
                config["io_json_dir"] = ioDir;
            }
        }

        private static void AddProviderKeys(JObject config, IReadOnlyDictionary<string, string> keys)
        {
            var provider = new JObject();
            foreach (var kvp in keys ?? new Dictionary<string, string>())
            {
                var providerKey = ProviderKeys.GetByName(kvp.Key);
                if (providerKey != null)
                {
                    provider[providerKey.ConfigField] = kvp.Value;
                }
            }

            if (provider.Count > 0)
            {
                config["provider"] = provider;
            }
        }

        private static void AddBedrock(JObject config, ISettingsProvider settings)
        {
            var options = new JObject();
            AddIfPresent(options, "profile", settings?.BedrockProfile);
            AddIfPresent(options, "region", settings?.BedrockRegion);

            if (options.Count == 0)
            {
                return;
            }

            config["opencode_config"] = new JObject
            {
                ["provider"] = new JObject
                {
                    ["amazon-bedrock"] = new JObject
                    {
                        ["options"] = options,
                    },
                },
            };
        }

        private static void AddIfPresent(JObject target, string name, string value)
        {
            var trimmed = value?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                target[name] = trimmed;
            }
        }
    }
}
