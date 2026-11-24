// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentModelsTests
    {
        [TestMethod]
        public void Catalog_Has19ModelsInVsCodeOrder()
        {
            var expectedIds = new[]
            {
                "anthropic/claude-sonnet-5-5",
                "anthropic/claude-opus-5-5",
                "anthropic/claude-fable-5-2",
                "anthropic/claude-haiku-5-5",
                "openai/gpt-6.1-sol",
                "openai/gpt-6-astra",
                "openai/gpt-6.1-terra",
                "openai/gpt-6-luna",
                "google/gemini-4-carbon",
                "google/gemini-4-argon",
                "google/gemini-4-hydrogen",
                "google/gemini-3.8-flash",
                "amazon-bedrock/eu.anthropic.claude-sonnet-5-5",
                "amazon-bedrock/eu.anthropic.claude-opus-5-5",
                "amazon-bedrock/eu.anthropic.claude-haiku-5-5",
                "amazon-bedrock/us.anthropic.claude-sonnet-5-5",
                "amazon-bedrock/us.anthropic.claude-opus-5-5",
                "amazon-bedrock/us.anthropic.claude-haiku-5-5",
                "amazon-bedrock/global.anthropic.claude-fable-5-2",
            };

            CollectionAssert.AreEqual(expectedIds, (System.Collections.ICollection)AgentModels.AllIds);
        }

        [TestMethod]
        public void Default_IsClaudeSonnet55()
        {
            var defaultValue = AgentModels.Default;
            Assert.AreEqual("anthropic/claude-sonnet-5-5", defaultValue);
        }

        [TestMethod]
        public void DisplayText_ThenIdFromDisplayText_RoundTrips()
        {
            var testedCount = 0;
            foreach (var id in AgentModels.AllIds)
            {
                var displayText = AgentModels.DisplayText(id);
                var roundTripId = AgentModels.IdFromDisplayText(displayText);
                Assert.AreEqual(id, roundTripId, $"Round trip failed for {id}");
                testedCount++;
            }

            Assert.AreEqual(19, testedCount, "Expected to test all 19 models");
        }

        [TestMethod]
        public void IdFromDisplayText_BareId_ReturnsId()
        {
            var id = "anthropic/claude-sonnet-5-5";
            var result = AgentModels.IdFromDisplayText(id);
            Assert.AreEqual(id, result);
        }

        [TestMethod]
        public void Descriptions_MatchVsCode()
        {
            Assert.AreEqual(
                "anthropic/claude-sonnet-5-5 — Claude Sonnet 5.5 via Anthropic API - Recommended",
                AgentModels.DisplayText("anthropic/claude-sonnet-5-5"));

            Assert.AreEqual(
                "amazon-bedrock/eu.anthropic.claude-sonnet-5-5 — Claude Sonnet 5.5 via Amazon Bedrock (EU)",
                AgentModels.DisplayText("amazon-bedrock/eu.anthropic.claude-sonnet-5-5"));

            Assert.AreEqual(
                "amazon-bedrock/global.anthropic.claude-fable-5-2 — Claude Fable 5.2 via Amazon Bedrock (Global) - Most capable, highest cost",
                AgentModels.DisplayText("amazon-bedrock/global.anthropic.claude-fable-5-2"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("   ")]
        public void IdFromDisplayText_BlankText_ReturnedAsIs(string? text)
        {
            Assert.AreEqual(text, AgentModels.IdFromDisplayText(text));
        }
    }
}
