// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Application.Security;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class ProviderKeyStoreTests
{
    private InMemoryProviderKeyBackingStore _backingStore;
    private Mock<ILogger> _mockLogger;
    private ProviderKeyStore _store;

    [TestInitialize]
    public void Setup()
    {
        _backingStore = new InMemoryProviderKeyBackingStore();
        _mockLogger = new Mock<ILogger>();
        _store = new ProviderKeyStore(_backingStore, _mockLogger.Object);
    }

    [TestMethod]
    public void Store_TrimsAndProtects()
    {
        const string plainKey = "my-api-key";
        const string withWhitespace = "  my-api-key  ";

        _store.Store("anthropic", withWhitespace);

        var backingValue = _backingStore.Get("anthropic:api-key");
        Assert.IsNotNull(backingValue);
        Assert.AreNotEqual(plainKey, backingValue);
        Assert.IsTrue(ProviderKeyProtector.TryUnprotect(backingValue, out var decrypted));
        Assert.AreEqual(plainKey, decrypted);
    }

    [TestMethod]
    public void Store_Empty_Deletes()
    {
        _store.Store("anthropic", "some-key");
        Assert.IsTrue(_backingStore.Has("anthropic:api-key"));

        _store.Store("anthropic", string.Empty);
        Assert.IsFalse(_backingStore.Has("anthropic:api-key"));

        _store.Store("openai", "another-key");
        Assert.IsTrue(_backingStore.Has("openai:api-key"));

        _store.Store("openai", "   ");
        Assert.IsFalse(_backingStore.Has("openai:api-key"));

        _store.Store("google", "yet-another-key");
        Assert.IsTrue(_backingStore.Has("google:api-key"));

        _store.Store("google", string.Empty);
        Assert.IsFalse(_backingStore.Has("google:api-key"));
    }

    [TestMethod]
    public void Get_CorruptBlob_ReturnsNullAndWarnsWithoutValue()
    {
        _backingStore.Set("anthropic:api-key", "corrupt-base64-blob");

        var result = _store.Get("anthropic");

        Assert.IsNull(result);
        _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("anthropic") && !s.Contains("corrupt"))), Times.Once);
    }

    [TestMethod]
    public void GetAll_ReturnsOnlyStored()
    {
        _store.Store("anthropic", "anthropic-key");
        _store.Store("google", "google-key");

        var all = _store.GetAll();

        Assert.AreEqual(2, all.Count);
        Assert.AreEqual("anthropic-key", all["anthropic"]);
        Assert.AreEqual("google-key", all["google"]);
        Assert.IsFalse(all.ContainsKey("openai"));
    }

    [TestMethod]
    public void IsStored_ReflectsBackingStore()
    {
        Assert.IsFalse(_store.IsStored("anthropic"));

        _store.Store("anthropic", "test-key");
        Assert.IsTrue(_store.IsStored("anthropic"));

        _store.Store("anthropic", string.Empty);
        Assert.IsFalse(_store.IsStored("anthropic"));
    }

    [TestMethod]
    public void IsStored_CorruptBlob_ReturnsFalseAndWarns()
    {
        _backingStore.Set("openai:api-key", "corrupt-blob");

        var result = _store.IsStored("openai");

        Assert.IsFalse(result);
        _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("openai"))), Times.Once);
    }

    [TestMethod]
    public void Get_NoDecryptCaching()
    {
        _store.Store("anthropic", "key-value");

        var result1 = _store.Get("anthropic");
        var result2 = _store.Get("anthropic");

        Assert.AreEqual("key-value", result1);
        Assert.AreEqual("key-value", result2);
        Assert.AreNotSame(result1, result2);
    }

    [TestMethod]
    public void UnknownProvider_IsIgnored()
    {
        _store.Store("unknown-provider", "some-key");

        Assert.IsFalse(_store.IsStored("unknown-provider"));
        Assert.IsNull(_store.Get("unknown-provider"));
        Assert.AreEqual(0, _store.GetAll().Count);
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

        public bool Has(string name)
        {
            return _storage.ContainsKey(name);
        }
    }
}
