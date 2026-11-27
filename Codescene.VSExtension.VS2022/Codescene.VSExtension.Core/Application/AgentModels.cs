// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Application
{
    public static class AgentModels
    {
        public const string Default = "anthropic/claude-sonnet-5-5";

        private static readonly (string Id, string Description)[] Models =
        {
            ("anthropic/claude-sonnet-5-5", "Claude Sonnet 5.5 via Anthropic API - Recommended"),
            ("anthropic/claude-opus-5-5", "Claude Opus 5.5 via Anthropic API - Most capable Opus"),
            ("anthropic/claude-fable-5-2", "Claude Fable 5.2 via Anthropic API - Most capable, highest cost"),
            ("anthropic/claude-haiku-5-5", "Claude Haiku 5.5 via Anthropic API - Fast and lightweight"),
            ("openai/gpt-6.1-sol", "GPT-6.1 Sol via OpenAI API"),
            ("openai/gpt-6-astra", "GPT-6 Astra via OpenAI API - Most capable, highest cost"),
            ("openai/gpt-6.1-terra", "GPT-6.1 Terra via OpenAI API - Lower cost"),
            ("openai/gpt-6-luna", "GPT-6 Luna via OpenAI API - Fast and lightweight"),
            ("google/gemini-4-carbon", "Gemini 4 Carbon via Google API"),
            ("google/gemini-4-argon", "Gemini 4 Argon via Google API"),
            ("google/gemini-4-hydrogen", "Gemini 4 Hydrogen via Google API - Lower cost"),
            ("google/gemini-3.8-flash", "Gemini 3.8 Flash via Google API - Fast and efficient"),
            ("amazon-bedrock/eu.anthropic.claude-sonnet-5-5", "Claude Sonnet 5.5 via Amazon Bedrock (EU)"),
            ("amazon-bedrock/eu.anthropic.claude-opus-5-5", "Claude Opus 5.5 via Amazon Bedrock (EU)"),
            ("amazon-bedrock/eu.anthropic.claude-haiku-5-5", "Claude Haiku 5.5 via Amazon Bedrock (EU) - Fast and lightweight"),
            ("amazon-bedrock/us.anthropic.claude-sonnet-5-5", "Claude Sonnet 5.5 via Amazon Bedrock (US)"),
            ("amazon-bedrock/us.anthropic.claude-opus-5-5", "Claude Opus 5.5 via Amazon Bedrock (US)"),
            ("amazon-bedrock/us.anthropic.claude-haiku-5-5", "Claude Haiku 5.5 via Amazon Bedrock (US) - Fast and lightweight"),
            ("amazon-bedrock/global.anthropic.claude-fable-5-2", "Claude Fable 5.2 via Amazon Bedrock (Global) - Most capable, highest cost"),
        };

        public static IReadOnlyList<string> AllIds => Models.Select(m => m.Id).ToList();

        public static string DisplayText(string id)
        {
            var model = Models.FirstOrDefault(m => m.Id == id);
            return model.Id != null ? $"{model.Id} — {model.Description}" : id;
        }

        public static string IdFromDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var model = Models.FirstOrDefault(m => DisplayText(m.Id) == text);
            if (model.Id != null)
            {
                return model.Id;
            }

            model = Models.FirstOrDefault(m => m.Id == text);
            return model.Id ?? text;
        }
    }
}
