// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Security;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class ProviderKeyProtectorTests
{
    [TestMethod]
    public void Protect_ThenUnprotect_RoundTrips()
    {
        const string key = "test-api-key-value";

        var protectedKey = ProviderKeyProtector.Protect(key);
        var success = ProviderKeyProtector.TryUnprotect(protectedKey, out var restored);

        Assert.IsTrue(success);
        Assert.AreNotEqual(key, protectedKey);
        Assert.AreEqual(key, restored);
    }

    [TestMethod]
    public void Unprotect_AuthTokenBlob_Fails()
    {
        const string token = "test-token-value";
        var authTokenBlob = AuthTokenProtector.Protect(token);

        var success = ProviderKeyProtector.TryUnprotect(authTokenBlob, out var plaintext);

        Assert.IsFalse(success);
        Assert.IsNull(plaintext);
    }

    [TestMethod]
    public void Unprotect_Plaintext_Fails()
    {
        const string plaintext = "plaintext-api-key";

        var success = ProviderKeyProtector.TryUnprotect(plaintext, out var result);

        Assert.IsFalse(success);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Protect_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, ProviderKeyProtector.Protect(string.Empty));
        Assert.AreEqual(string.Empty, ProviderKeyProtector.Protect(null));
    }

    [TestMethod]
    public void TryUnprotect_Empty_ReturnsEmptyAndTrue()
    {
        var success1 = ProviderKeyProtector.TryUnprotect(string.Empty, out var result1);
        var success2 = ProviderKeyProtector.TryUnprotect(null, out var result2);

        Assert.IsTrue(success1);
        Assert.AreEqual(string.Empty, result1);
        Assert.IsTrue(success2);
        Assert.AreEqual(string.Empty, result2);
    }
}
