// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Security;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class AuthTokenProtectorTests
{
    [TestMethod]
    public void Protect_Unprotect_RoundTrips()
    {
        const string token = "test-token-value";

        var protectedToken = AuthTokenProtector.Protect(token);
        var restored = AuthTokenProtector.Unprotect(protectedToken);

        Assert.AreNotEqual(token, protectedToken);
        Assert.AreEqual(token, restored);
    }

    [TestMethod]
    public void Protect_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, AuthTokenProtector.Protect(string.Empty));
        Assert.AreEqual(string.Empty, AuthTokenProtector.Protect(null!));
    }

    [TestMethod]
    public void Unprotect_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, AuthTokenProtector.Unprotect(string.Empty));
        Assert.AreEqual(string.Empty, AuthTokenProtector.Unprotect(null!));
    }

    [TestMethod]
    public void Unprotect_LegacyPlaintext_ReturnsUnchanged()
    {
        const string legacyToken = "legacy-plaintext-token";

        Assert.AreEqual(legacyToken, AuthTokenProtector.Unprotect(legacyToken));
    }
}
