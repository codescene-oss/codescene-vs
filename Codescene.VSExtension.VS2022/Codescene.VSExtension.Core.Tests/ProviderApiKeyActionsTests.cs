// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Application.Security;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Agent;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class ProviderApiKeyActionsTests
{
    [TestMethod]
    public void Items_MarkStoredProviders()
    {
        var backingStore = new InMemoryProviderKeyBackingStore();
        var mockLogger = new Mock<ILogger>();
        var store = new ProviderKeyStore(backingStore, mockLogger.Object);

        store.Store("anthropic", "anthropic-key");
        store.Store("google", "google-key");

        var items = ProviderApiKeyActions.Items(store);

        Assert.AreEqual(3, items.Count);

        var anthropicItem = items.First(i => i.Provider.Name == "anthropic");
        Assert.IsTrue(anthropicItem.KeyStored);
        Assert.AreEqual("Anthropic", anthropicItem.Provider.Label);

        var openaiItem = items.First(i => i.Provider.Name == "openai");
        Assert.IsFalse(openaiItem.KeyStored);
        Assert.AreEqual("OpenAI", openaiItem.Provider.Label);

        var googleItem = items.First(i => i.Provider.Name == "google");
        Assert.IsTrue(googleItem.KeyStored);
        Assert.AreEqual("Google (Gemini)", googleItem.Provider.Label);
    }

    [TestMethod]
    public void Apply_Value_StoresAndReportsStored()
    {
        var backingStore = new InMemoryProviderKeyBackingStore();
        var mockLogger = new Mock<ILogger>();
        var store = new ProviderKeyStore(backingStore, mockLogger.Object);

        var result = ProviderApiKeyActions.Apply(store, "anthropic", "my-api-key");

        Assert.AreEqual("Stored the Anthropic API key securely.", result);
        Assert.IsTrue(store.IsStored("anthropic"));
        Assert.AreEqual("my-api-key", store.Get("anthropic"));
    }

    [TestMethod]
    public void Apply_Empty_RemovesAndReportsRemoved()
    {
        var backingStore = new InMemoryProviderKeyBackingStore();
        var mockLogger = new Mock<ILogger>();
        var store = new ProviderKeyStore(backingStore, mockLogger.Object);

        store.Store("openai", "existing-key");
        Assert.IsTrue(store.IsStored("openai"));

        var result = ProviderApiKeyActions.Apply(store, "openai", string.Empty);

        Assert.AreEqual("Removed the OpenAI API key.", result);
        Assert.IsFalse(store.IsStored("openai"));
    }

    [TestMethod]
    public void Apply_Whitespace_RemovesAndReportsRemoved()
    {
        var backingStore = new InMemoryProviderKeyBackingStore();
        var mockLogger = new Mock<ILogger>();
        var store = new ProviderKeyStore(backingStore, mockLogger.Object);

        store.Store("google", "existing-key");

        var result = ProviderApiKeyActions.Apply(store, "google", "   ");

        Assert.AreEqual("Removed the Google (Gemini) API key.", result);
        Assert.IsFalse(store.IsStored("google"));
    }

    [TestMethod]
    public void Apply_UnknownProvider_ReturnsNull()
    {
        var backingStore = new InMemoryProviderKeyBackingStore();
        var mockLogger = new Mock<ILogger>();
        var store = new ProviderKeyStore(backingStore, mockLogger.Object);

        var result = ProviderApiKeyActions.Apply(store, "unknown-provider", "key");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ConnectivityReport_Success()
    {
        var result = new ConnectivityResult
        {
            Success = true,
        };

        var report = ProviderApiKeyActions.ConnectivityReport(result);

        Assert.AreEqual("Provider connectivity test successful.", report);
    }

    [TestMethod]
    public void ConnectivityReport_Failure()
    {
        var result = new ConnectivityResult
        {
            Error = "Authentication failed",
        };

        var report = ProviderApiKeyActions.ConnectivityReport(result);

        Assert.AreEqual("Provider connectivity test failed: Authentication failed", report);
    }

    [TestMethod]
    public void ConnectivityReport_Cancelled_IsNull()
    {
        var result = new ConnectivityResult
        {
            Cancelled = true,
        };

        var report = ProviderApiKeyActions.ConnectivityReport(result);

        Assert.IsNull(report);
    }

    [TestMethod]
    public void ConnectivityReport_NullResult_IsNull()
    {
        var report = ProviderApiKeyActions.ConnectivityReport(null);

        Assert.IsNull(report);
    }

    private class InMemoryProviderKeyBackingStore : IProviderKeyBackingStore
    {
        private readonly Dictionary<string, string> _storage = new Dictionary<string, string>();

        public string Get(string name)
        {
            return _storage.TryGetValue(name, out var value) ? value : null!;
        }

        public void Set(string name, string value)
        {
            _storage[name] = value;
        }

        public void Delete(string name)
        {
            _storage.Remove(name);
        }
    }
}
