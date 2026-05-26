// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class ExternalNavigationAllowlistTests
{
    [TestMethod]
    [DataRow("https://codescene.io/docs")]
    [DataRow("https://www.codescene.com")]
    [DataRow("https://en.wikipedia.org/wiki/Test")]
    [DataRow("https://blog.ploeh.dk/2018/08/27/on-constructor-over-injection/")]
    [DataRow("https://helpcenter.codescene.com/hc/en-us")]
    public void IsAllowedExternalUri_AllowedHosts_ReturnsTrue(string uri)
        => Assert.IsTrue(ExternalNavigationAllowlist.IsAllowedExternalUri(uri));

    [TestMethod]
    [DataRow("https://codescene.io.evil.example/phish")]
    [DataRow("https://codescene.com@attacker.tld/")]
    [DataRow("http://codescene.io")]
    [DataRow("file:///etc/passwd")]
    [DataRow("https://evilcodescene.io")]
    [DataRow("https://notcodescene.io")]
    [DataRow("")]
    [DataRow("not-a-uri")]
    public void IsAllowedExternalUri_DisallowedUris_ReturnsFalse(string uri)
        => Assert.IsFalse(ExternalNavigationAllowlist.IsAllowedExternalUri(uri));
}
