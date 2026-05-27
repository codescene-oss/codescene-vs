// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class SecretBufferTests
{
    [TestMethod]
    public void Equals_EmptyAndNull_AreEqual()
    {
        Assert.IsTrue(SecretBuffer.Equals(null, string.Empty));
        Assert.IsTrue(SecretBuffer.Equals(null, null!));
    }

    [TestMethod]
    public void Equals_SameValue_ReturnsTrue()
    {
        var stored = SecretBuffer.FromString("token-a");
        Assert.IsTrue(SecretBuffer.Equals(stored, "token-a"));
        SecretBuffer.Clear(ref stored);
    }

    [TestMethod]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var stored = SecretBuffer.FromString("token-a");
        Assert.IsFalse(SecretBuffer.Equals(stored, "token-b"));
        SecretBuffer.Clear(ref stored);
    }

    [TestMethod]
    public void Replace_ClearsPreviousBuffer()
    {
        byte[] stored = SecretBuffer.FromString("old");
        var previousReference = stored;
        SecretBuffer.Replace(ref stored, "new");
        Assert.AreNotSame(previousReference, stored);
        Assert.IsTrue(SecretBuffer.Equals(stored, "new"));
        SecretBuffer.Clear(ref stored);
        foreach (var b in previousReference)
        {
            Assert.AreEqual(0, b);
        }
    }
}
