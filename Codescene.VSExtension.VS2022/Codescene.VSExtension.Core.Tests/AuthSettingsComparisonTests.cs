// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Application.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthSettingsComparisonTests
    {
        [TestMethod]
        public void Detect_PatChanged_FiresAuthTokenChanged()
        {
            var prevPat = SecretBuffer.FromString("old-token");
            var currPat = "new-token";

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: false,
                prevServerUrl: string.Empty,
                currServerUrl: string.Empty,
                prevPat: prevPat,
                currPat: currPat);

            Assert.IsTrue(patChanged, "PatChanged should be true when PAT changes");
            Assert.IsTrue(authSettingsChanged, "AuthSettingsChanged should be true when PAT changes");
        }

        [TestMethod]
        public void Detect_PatUnchanged_DoesNotFireAuthTokenChanged()
        {
            var token = "same-token";
            var prevPat = SecretBuffer.FromString(token);

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: false,
                prevServerUrl: string.Empty,
                currServerUrl: string.Empty,
                prevPat: prevPat,
                currPat: token);

            Assert.IsFalse(patChanged, "PatChanged should be false when PAT is unchanged");
            Assert.IsFalse(authSettingsChanged, "AuthSettingsChanged should be false when no settings change");
        }

        [TestMethod]
        public void Detect_BrowserSignInChanged_FiresOnlyAuthSettingsChanged()
        {
            var token = "same-token";
            var prevPat = SecretBuffer.FromString(token);

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: true,
                prevServerUrl: string.Empty,
                currServerUrl: string.Empty,
                prevPat: prevPat,
                currPat: token);

            Assert.IsFalse(patChanged, "PatChanged should be false when only BrowserSignIn changes");
            Assert.IsTrue(authSettingsChanged, "AuthSettingsChanged should be true when BrowserSignIn changes");
        }

        [TestMethod]
        public void Detect_ServerUrlChanged_FiresOnlyAuthSettingsChanged()
        {
            var token = "same-token";
            var prevPat = SecretBuffer.FromString(token);

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: false,
                prevServerUrl: "https://old.example.com",
                currServerUrl: "https://new.example.com",
                prevPat: prevPat,
                currPat: token);

            Assert.IsFalse(patChanged, "PatChanged should be false when only ServerUrl changes");
            Assert.IsTrue(authSettingsChanged, "AuthSettingsChanged should be true when ServerUrl changes");
        }

        [TestMethod]
        public void Detect_ServerUrlUnchangedAfterNormalization_DoesNotFire()
        {
            var token = "same-token";
            var prevPat = SecretBuffer.FromString(token);

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: false,
                prevServerUrl: "https://example.com",
                currServerUrl: "  https://example.com  ",
                prevPat: prevPat,
                currPat: token);

            Assert.IsFalse(patChanged, "PatChanged should be false when only whitespace differs");
            Assert.IsFalse(authSettingsChanged, "AuthSettingsChanged should be false when ServerUrl normalizes to same value");
        }

        [TestMethod]
        public void Detect_MultipleSettingsChanged_FiresBothOnce()
        {
            var prevPat = SecretBuffer.FromString("old-token");
            var currPat = "new-token";

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: true,
                prevServerUrl: "https://old.example.com",
                currServerUrl: "https://new.example.com",
                prevPat: prevPat,
                currPat: currPat);

            Assert.IsTrue(patChanged, "PatChanged should be true when PAT changes");
            Assert.IsTrue(authSettingsChanged, "AuthSettingsChanged should be true when multiple settings change");
        }

        [TestMethod]
        public void Detect_EmptyAndNullServerUrlsAreEquivalent()
        {
            var token = "same-token";
            var prevPat = SecretBuffer.FromString(token);

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: false,
                prevServerUrl: string.Empty,
                currServerUrl: null,
                prevPat: prevPat,
                currPat: token);

            Assert.IsFalse(patChanged, "PatChanged should be false");
            Assert.IsFalse(authSettingsChanged, "AuthSettingsChanged should be false when empty and null ServerUrl are compared");
        }

        [TestMethod]
        public void Detect_WhitespaceOnlyServerUrlBecomesNull()
        {
            var token = "same-token";
            var prevPat = SecretBuffer.FromString(token);

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: false,
                prevServerUrl: string.Empty,
                currServerUrl: "   ",
                prevPat: prevPat,
                currPat: token);

            Assert.IsFalse(patChanged, "PatChanged should be false");
            Assert.IsFalse(authSettingsChanged, "AuthSettingsChanged should be false when whitespace-only ServerUrl normalizes to null");
        }

        [TestMethod]
        public void Detect_EmptyAndNullPatsAreEquivalent()
        {
            var prevPat = SecretBuffer.FromString(string.Empty);

            var (patChanged, authSettingsChanged) = AuthSettingsComparison.Detect(
                prevBrowserSignIn: false,
                currBrowserSignIn: false,
                prevServerUrl: string.Empty,
                currServerUrl: string.Empty,
                prevPat: prevPat,
                currPat: null);

            Assert.IsFalse(patChanged, "PatChanged should be false when empty and null PAT are compared");
            Assert.IsFalse(authSettingsChanged, "AuthSettingsChanged should be false");
        }
    }
}
