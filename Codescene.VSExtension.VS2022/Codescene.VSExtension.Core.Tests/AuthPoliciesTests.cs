// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthPoliciesTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void ServerUrl_Blank_IsNull()
        {
            var result = AuthConfiguration.ServerUrl(string.Empty);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ServerUrl_Whitespace_IsNull()
        {
            var result = AuthConfiguration.ServerUrl("   ");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ServerUrl_Value_IsTrimmed()
        {
            var result = AuthConfiguration.ServerUrl("  https://codescene.io  ");

            Assert.AreEqual("https://codescene.io", result);
        }

        [TestMethod]
        public void AccessToken_Blank_IsNull()
        {
            var result = AuthConfiguration.AccessToken(string.Empty);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void AccessToken_Whitespace_IsNull()
        {
            var result = AuthConfiguration.AccessToken("   ");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void AccessToken_Value_IsTrimmed()
        {
            var result = AuthConfiguration.AccessToken("  token123  ");

            Assert.AreEqual("token123", result);
        }

        [TestMethod]
        public void SignInOutcome_NotFromSigningIn_IsNull()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            var result = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void SignInOutcome_StillSigningIn_IsNull()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);

            var result = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void SignInOutcome_SignedInNoError_IsCompleted()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            var result = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);

            Assert.IsNotNull(result);
            Assert.AreEqual(Constants.Telemetry.AUTHSIGNINCOMPLETED, result.Value.Key);
            Assert.IsNull(result.Value.Value);
        }

        [TestMethod]
        public void SignInOutcome_Cancelled_IsCancelled()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signed-out-with-error", FixtureForm.Wire);
            nextStatus["error"]["code"] = "auth/cancelled";

            var result = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);

            Assert.IsNotNull(result);
            Assert.AreEqual(Constants.Telemetry.AUTHSIGNINCANCELLED, result.Value.Key);
            Assert.IsNull(result.Value.Value);
        }

        [TestMethod]
        public void SignInOutcome_Timeout_IsFailedWithCode()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signed-out-timeout", FixtureForm.Wire);

            var result = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);

            Assert.IsNotNull(result);
            Assert.AreEqual(Constants.Telemetry.AUTHSIGNINFAILED, result.Value.Key);
            Assert.IsNotNull(result.Value.Value);
            Assert.AreEqual("auth/timeout", result.Value.Value["error-code"]);
        }

        [TestMethod]
        public void SignInOutcome_SignedOutNoError_IsFailedUnknown()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);

            var result = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);

            Assert.IsNotNull(result);
            Assert.AreEqual(Constants.Telemetry.AUTHSIGNINFAILED, result.Value.Key);
            Assert.IsNotNull(result.Value.Value);
            Assert.AreEqual("unknown", result.Value.Value["error-code"]);
        }

        [TestMethod]
        public void TelemetryEvents_CarryNoIdentifiers()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signed-out-timeout", FixtureForm.Wire);

            var outcome = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);
            var failed = AuthTelemetryEvents.SignInFailed("test-code");
            var reauthorizeFailed = AuthTelemetryEvents.ReauthorizeFailed("test-code");

            Assert.IsNotNull(outcome);
            Assert.IsTrue(outcome.Value.Value.Count == 1 && outcome.Value.Value.ContainsKey("error-code"));
            Assert.IsTrue(failed.Value.Count == 1 && failed.Value.ContainsKey("error-code"));
            Assert.IsTrue(reauthorizeFailed.Value.Count == 1 && reauthorizeFailed.Value.ContainsKey("error-code"));
        }

        [TestMethod]
        public void IsOpenable_Https_IsTrue()
        {
            var result = AuthUrlPolicy.IsOpenable("https://codescene.io");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsOpenable_Http_IsTrue()
        {
            var result = AuthUrlPolicy.IsOpenable("http://localhost:8080");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsOpenable_Javascript_IsFalse()
        {
            var result = AuthUrlPolicy.IsOpenable("javascript:alert('test')");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsOpenable_File_IsFalse()
        {
            var result = AuthUrlPolicy.IsOpenable("file:///c:/test.txt");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsOpenable_Relative_IsFalse()
        {
            var result = AuthUrlPolicy.IsOpenable("/path/to/resource");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsOpenable_Null_IsFalse()
        {
            var result = AuthUrlPolicy.IsOpenable(null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsOpenable_Empty_IsFalse()
        {
            var result = AuthUrlPolicy.IsOpenable(string.Empty);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsOpenable_Whitespace_IsFalse()
        {
            var result = AuthUrlPolicy.IsOpenable("   ");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ServerUrl_Null_IsNull()
        {
            var result = AuthConfiguration.ServerUrl(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void AccessToken_Null_IsNull()
        {
            var result = AuthConfiguration.AccessToken(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void SignInFailed_WithNullCode_ReturnsUnknown()
        {
            var result = AuthTelemetryEvents.SignInFailed(null);

            Assert.AreEqual(Constants.Telemetry.AUTHSIGNINFAILED, result.Key);
            Assert.AreEqual("unknown", result.Value["error-code"]);
        }

        [TestMethod]
        public void ReauthorizeFailed_WithNullCode_ReturnsUnknown()
        {
            var result = AuthTelemetryEvents.ReauthorizeFailed(null);

            Assert.AreEqual(Constants.Telemetry.AUTHREAUTHORIZEFAILED, result.Key);
            Assert.AreEqual("unknown", result.Value["error-code"]);
        }

        [TestMethod]
        public void SignInOutcome_SignedInWithError_IsFailed()
        {
            var previousStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var nextStatus = (JObject)_bundle.Fixture("auth-status", "signed-in-with-error", FixtureForm.Wire);

            var result = AuthTelemetryEvents.SignInOutcome(previousStatus, nextStatus);

            Assert.IsNotNull(result);
            Assert.AreEqual(Constants.Telemetry.AUTHSIGNINFAILED, result.Value.Key);
            Assert.IsNotNull(result.Value.Value);
        }
    }
}
