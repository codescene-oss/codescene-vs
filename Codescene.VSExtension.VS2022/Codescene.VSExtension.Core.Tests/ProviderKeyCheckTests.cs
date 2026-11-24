// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ProviderKeyCheckTests
    {
        [TestMethod]
        public void StoredKey_Passes()
        {
            var storedProviders = new List<string> { "anthropic" };

            var result = ProviderKeyCheck.FindMissing("anthropic/claude-sonnet-5-5", storedProviders, EmptyEnv);

            Assert.IsNull(result);
        }

        [TestMethod]
        [DataRow("ANTHROPIC_API_KEY", "anthropic/claude-sonnet-5-5")]
        [DataRow("OPENAI_API_KEY", "openai/gpt-6.1-sol")]
        [DataRow("GOOGLE_API_KEY", "google/gemini-4-carbon")]
        [DataRow("GOOGLE_GENERATIVE_AI_API_KEY", "google/gemini-4-carbon")]
        [DataRow("GEMINI_API_KEY", "google/gemini-4-carbon")]
        public void EnvVar_Passes(string envVarName, string model)
        {
            var storedProviders = new List<string>();
            string Env(string name) => name == envVarName ? "key-value" : string.Empty;

            var result = ProviderKeyCheck.FindMissing(model, storedProviders, Env);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Missing_ReturnsProvider()
        {
            var storedProviders = new List<string>();

            var result = ProviderKeyCheck.FindMissing("anthropic/claude-sonnet-5-5", storedProviders, EmptyEnv);

            Assert.AreEqual("anthropic", result);
        }

        [TestMethod]
        public void OverrideWithKnownPrefix_IsChecked()
        {
            var storedProviders = new List<string>();

            var result = ProviderKeyCheck.FindMissing("openai/custom-model-x", storedProviders, EmptyEnv);

            Assert.AreEqual("openai", result);
        }

        [TestMethod]
        [DataRow("amazon-bedrock/eu.anthropic.claude-sonnet-5-5")]
        [DataRow("custom-provider/some-model")]
        public void BedrockOrCustomPrefix_NotChecked(string model)
        {
            var storedProviders = new List<string>();

            var result = ProviderKeyCheck.FindMissing(model, storedProviders, EmptyEnv);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void MissingMessage_MatchesSpecText()
        {
            var anthropicMessage = ProviderKeyCheck.MissingMessage("anthropic", "anthropic/claude-sonnet-5-5");
            Assert.AreEqual(
                "No Anthropic API key is set for anthropic/claude-sonnet-5-5. Set one in Tools > Options > CodeScene > General > Refactoring Agent.",
                anthropicMessage);

            var openaiMessage = ProviderKeyCheck.MissingMessage("openai", "openai/gpt-6.1-sol");
            Assert.AreEqual(
                "No OpenAI API key is set for openai/gpt-6.1-sol. Set one in Tools > Options > CodeScene > General > Refactoring Agent.",
                openaiMessage);

            var googleMessage = ProviderKeyCheck.MissingMessage("google", "google/gemini-4-carbon");
            Assert.AreEqual(
                "No Google (Gemini) API key is set for google/gemini-4-carbon. Set one in Tools > Options > CodeScene > General > Refactoring Agent.",
                googleMessage);
        }

        [TestMethod]
        public void EnvVar_WhitespaceValue_TreatedAsMissing()
        {
            var storedProviders = new List<string>();
            string Env(string name) => name == "ANTHROPIC_API_KEY" ? "   " : string.Empty;

            var result = ProviderKeyCheck.FindMissing("anthropic/claude-sonnet-5-5", storedProviders, Env);

            Assert.AreEqual("anthropic", result);
        }

        [TestMethod]
        public void NullModel_ReturnsNull()
        {
            var storedProviders = new List<string>();

            var result = ProviderKeyCheck.FindMissing(null, storedProviders, EmptyEnv);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ModelWithoutSlash_ReturnsNull()
        {
            var storedProviders = new List<string>();

            var result = ProviderKeyCheck.FindMissing("invalid-model", storedProviders, EmptyEnv);

            Assert.IsNull(result);
        }

        private static string EmptyEnv(string name) => string.Empty;
    }
}
