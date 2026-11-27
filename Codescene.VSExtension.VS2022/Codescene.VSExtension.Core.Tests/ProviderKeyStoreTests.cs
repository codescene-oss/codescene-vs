// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class ProviderKeyStoreTests
{
    [TestMethod]
    public void GetAll_ReturnsTheConfiguredKeysByProvider()
    {
        var store = CreateStore(anthropic: "anthropic-key", openAI: "openai-key", google: "google-key");

        var keys = store.GetAll();

        Assert.HasCount(3, keys);
        Assert.AreEqual("anthropic-key", keys["anthropic"]);
        Assert.AreEqual("openai-key", keys["openai"]);
        Assert.AreEqual("google-key", keys["google"]);
    }

    [TestMethod]
    public void GetAll_TrimsKeys()
    {
        var store = CreateStore(anthropic: "  anthropic-key  ");

        var keys = store.GetAll();

        Assert.AreEqual("anthropic-key", keys["anthropic"]);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GetAll_SkipsEmptyKeys(string value)
    {
        var store = CreateStore(anthropic: value, openAI: "openai-key", google: value);

        var keys = store.GetAll();

        Assert.HasCount(1, keys);
        Assert.IsTrue(keys.ContainsKey("openai"));
    }

    [TestMethod]
    public void GetAll_NothingConfigured_IsEmpty()
    {
        var store = CreateStore();

        Assert.IsEmpty(store.GetAll());
    }

    private static ProviderKeyStore CreateStore(string anthropic = "", string openAI = "", string google = "")
    {
        var settings = new Mock<ISettingsProvider>();
        settings.Setup(s => s.AnthropicApiKey).Returns(anthropic);
        settings.Setup(s => s.OpenAIApiKey).Returns(openAI);
        settings.Setup(s => s.GoogleApiKey).Returns(google);
        return new ProviderKeyStore(settings.Object);
    }
}
