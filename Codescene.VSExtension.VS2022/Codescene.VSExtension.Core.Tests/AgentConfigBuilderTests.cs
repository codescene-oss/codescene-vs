// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentConfigBuilderTests
    {
        [TestMethod]
        public void MinimalConfig_HasTokenPluginsTracking()
        {
            var config = AgentConfigBuilder.Build(
                "test-token",
                AgentSkill.RenderCodeFix,
                "/io/dir",
                null,
                new Dictionary<string, string>(),
                null);

            var json = JObject.Parse(config);

            Assert.AreEqual("test-token", json["codescene_access_token"].ToString());
            Assert.AreEqual("render-code-fix-reporter", json["plugins"][0].ToString());
            Assert.AreEqual("codescene-vs", json["tracking"]["environment"].ToString());
            Assert.AreEqual("/io/dir", json["io_json_dir"].ToString());
            Assert.IsNull(json["codescene_onprem_url"]);
            Assert.IsNull(json["provider"]);
            Assert.IsNull(json["opencode_config"]);
        }

        [TestMethod]
        public void Onprem_AddsUrl()
        {
            var status = new JObject
            {
                ["server"] = new JObject
                {
                    ["onprem"] = true,
                    ["url"] = "https://onprem.example.com",
                },
            };

            var snapshot = new AuthSnapshot(
                true,
                status,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false);

            var config = AgentConfigBuilder.Build(
                "test-token",
                AgentSkill.RenderCodeFix,
                "/io/dir",
                snapshot,
                new Dictionary<string, string>(),
                null);

            var json = JObject.Parse(config);

            Assert.AreEqual("https://onprem.example.com", json["codescene_onprem_url"].ToString());
        }

        [TestMethod]
        public void Cloud_OmitsUrl()
        {
            var status = new JObject
            {
                ["server"] = new JObject
                {
                    ["onprem"] = false,
                    ["url"] = "https://cloud.example.com",
                },
            };

            var snapshot = new AuthSnapshot(
                true,
                status,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false);

            var config = AgentConfigBuilder.Build(
                "test-token",
                AgentSkill.RenderCodeFix,
                "/io/dir",
                snapshot,
                new Dictionary<string, string>(),
                null);

            var json = JObject.Parse(config);

            Assert.IsNull(json["codescene_onprem_url"]);
        }

        [TestMethod]
        public void StoredKeys_OnlyStoredFields()
        {
            var keys = new Dictionary<string, string>
            {
                ["anthropic"] = "anthropic-key",
                ["google"] = "google-key",
            };

            var config = AgentConfigBuilder.Build(
                "test-token",
                AgentSkill.RenderCodeFix,
                "/io/dir",
                null,
                keys,
                null);

            var json = JObject.Parse(config);
            var provider = json["provider"] as JObject;

            Assert.IsNotNull(provider);
            Assert.AreEqual("anthropic-key", provider["anthropic_api_key"].ToString());
            Assert.AreEqual("google-key", provider["google_api_key"].ToString());
            Assert.IsNull(provider["openai_api_key"]);
        }

        [TestMethod]
        public void NoKeys_OmitsProvider()
        {
            var config = AgentConfigBuilder.Build(
                "test-token",
                AgentSkill.RenderCodeFix,
                "/io/dir",
                null,
                new Dictionary<string, string>(),
                null);

            var json = JObject.Parse(config);

            Assert.IsNull(json["provider"]);
        }

        [TestMethod]
        public void Bedrock_OnlySetOptions()
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.BedrockProfile).Returns("my-profile");
            settings.Setup(s => s.BedrockRegion).Returns(string.Empty);

            var config = AgentConfigBuilder.Build(
                "test-token",
                AgentSkill.RenderCodeFix,
                "/io/dir",
                null,
                new Dictionary<string, string>(),
                settings.Object);

            var json = JObject.Parse(config);
            var opencode = json["opencode_config"];
            var options = opencode["provider"]["amazon-bedrock"]["options"] as JObject;

            Assert.IsNotNull(options);
            Assert.AreEqual("my-profile", options["profile"].ToString());
            Assert.IsNull(options["region"]);
        }

        [TestMethod]
        public void Connectivity_OmitsIoDir()
        {
            var config = AgentConfigBuilder.Build(
                "test-token",
                AgentSkill.CheckConnectivity,
                null,
                null,
                new Dictionary<string, string>(),
                null);

            var json = JObject.Parse(config);

            Assert.IsNull(json["io_json_dir"]);
        }

        [TestMethod]
        public void MissingToken_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => AgentConfigBuilder.Build(string.Empty, AgentSkill.RenderCodeFix, null, null, null, null));
        }

        [TestMethod]
        public void MissingSkill_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => AgentConfigBuilder.Build("token", null, null, null, null, null));
        }
    }
}
