// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentModelResolverTests
    {
        [TestMethod]
        public void Override_Wins()
        {
            var settings = CreateSettingsMock(
                agentModel: "openai/gpt-6.1-sol",
                agentModelOverride: "custom/model-x");
            var storedProviders = new List<string> { "anthropic" };

            var result = AgentModelResolver.Resolve(settings.Object, storedProviders);

            Assert.AreEqual("custom/model-x", result);
        }

        [TestMethod]
        public void WhitespaceOverride_Ignored()
        {
            var settings = CreateSettingsMock(
                agentModel: "openai/gpt-6.1-sol",
                agentModelOverride: "   ");
            var storedProviders = new List<string>();

            var result = AgentModelResolver.Resolve(settings.Object, storedProviders);

            Assert.AreEqual("openai/gpt-6.1-sol", result);
        }

        [TestMethod]
        public void NonDefaultModel_Wins_OverInference()
        {
            var settings = CreateSettingsMock(agentModel: "google/gemini-4-carbon");
            var storedProviders = new List<string> { "anthropic" };

            var result = AgentModelResolver.Resolve(settings.Object, storedProviders);

            Assert.AreEqual("google/gemini-4-carbon", result);
        }

        [TestMethod]
        [DataRow("anthropic", "anthropic/claude-sonnet-5-5")]
        [DataRow("openai", "openai/gpt-6.1-sol")]
        [DataRow("google", "google/gemini-4-carbon")]
        public void DefaultModel_InfersFromFirstStoredKey(string provider, string expectedModel)
        {
            var settings = CreateSettingsMock();
            var storedProviders = new List<string> { provider };

            var result = AgentModelResolver.Resolve(settings.Object, storedProviders);

            Assert.AreEqual(expectedModel, result);
        }

        [TestMethod]
        public void DefaultModel_InfersFromFirstStoredKey_Precedence()
        {
            var settings = CreateSettingsMock();

            var anthropicFirst = new List<string> { "anthropic", "openai", "google" };
            var result1 = AgentModelResolver.Resolve(settings.Object, anthropicFirst);
            Assert.AreEqual("anthropic/claude-sonnet-5-5", result1);

            var openaiFirst = new List<string> { "openai", "google" };
            var result2 = AgentModelResolver.Resolve(settings.Object, openaiFirst);
            Assert.AreEqual("openai/gpt-6.1-sol", result2);

            var googleOnly = new List<string> { "google" };
            var result3 = AgentModelResolver.Resolve(settings.Object, googleOnly);
            Assert.AreEqual("google/gemini-4-carbon", result3);
        }

        [TestMethod]
        public void Bedrock_ProfileOrRegion_InfersBedrockModel()
        {
            var settingsWithProfile = CreateSettingsMock(bedrockProfile: "my-profile");
            var result1 = AgentModelResolver.Resolve(settingsWithProfile.Object, new List<string>());
            Assert.AreEqual("amazon-bedrock/eu.anthropic.claude-sonnet-5-5", result1);

            var settingsWithRegion = CreateSettingsMock(bedrockRegion: "us-west-2");
            var result2 = AgentModelResolver.Resolve(settingsWithRegion.Object, new List<string>());
            Assert.AreEqual("amazon-bedrock/eu.anthropic.claude-sonnet-5-5", result2);

            var settingsWithBoth = CreateSettingsMock(bedrockProfile: "my-profile", bedrockRegion: "us-west-2");
            var result3 = AgentModelResolver.Resolve(settingsWithBoth.Object, new List<string>());
            Assert.AreEqual("amazon-bedrock/eu.anthropic.claude-sonnet-5-5", result3);
        }

        [TestMethod]
        public void NothingConfigured_ReturnsDefault()
        {
            var settings = CreateSettingsMock();
            var storedProviders = new List<string>();

            var result = AgentModelResolver.Resolve(settings.Object, storedProviders);

            Assert.AreEqual(AgentModels.Default, result);
        }

        private Mock<ISettingsProvider> CreateSettingsMock(
            string agentModel = AgentModels.Default,
            string agentModelOverride = "",
            string bedrockProfile = "",
            string bedrockRegion = "")
        {
            var mock = new Mock<ISettingsProvider>();
            mock.Setup(s => s.AgentModel).Returns(agentModel);
            mock.Setup(s => s.AgentModelOverride).Returns(agentModelOverride);
            mock.Setup(s => s.BedrockProfile).Returns(bedrockProfile);
            mock.Setup(s => s.BedrockRegion).Returns(bedrockRegion);
            return mock;
        }
    }
}
