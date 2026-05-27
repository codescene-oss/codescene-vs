// Copyright (c) CodeScene. All rights reserved.

using System;
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
    public void Equals_NonEmptyLeft_EmptyRight_ReturnsFalse()
    {
        var stored = SecretBuffer.FromString("token");
        Assert.IsFalse(SecretBuffer.Equals(stored, string.Empty));
        SecretBuffer.Clear(ref stored);
    }

    [TestMethod]
    public void Equals_DifferentLengths_ReturnsFalse()
    {
        var stored = SecretBuffer.FromString("ab");
        Assert.IsFalse(SecretBuffer.Equals(stored, "abc"));
        SecretBuffer.Clear(ref stored);
    }

    [TestMethod]
    public void Clear_NullOrEmpty_SetsNull()
    {
        byte[] buffer = null;
        SecretBuffer.Clear(ref buffer);
        Assert.IsNull(buffer);

        buffer = Array.Empty<byte>();
        SecretBuffer.Clear(ref buffer);
        Assert.IsNull(buffer);
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
