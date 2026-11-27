// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class SignInNudgePolicyTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void ShouldNudge_SignedOut_IsTrue()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldNudge_EnvTokenPresent_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out-env-token-invalid", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldNudge_OAuthUnsupported_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out-oauth-unsupported", FixtureForm.Wire);
            status["error"] = JValue.CreateNull();
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldNudge_WithInvalidTokenError_IsTrue()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            status["error"] = new JObject
            {
                ["code"] = "auth/invalid-token",
                ["message"] = "Invalid token",
            };
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldNudge_WithOtherError_Timeout_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out-timeout", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldNudge_WithOtherError_WithError_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out-with-error", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldNudge_SignedIn_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldNudge_SigningIn_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldNudge_Inactive_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithStatus(status);

            var result = SignInNudgePolicy.ShouldNudge(snapshot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldShow_AfterShown_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldShow(snapshot, shownThisSession: true, dismissed: false);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldShow_AfterDismissed_IsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldShow(snapshot, shownThisSession: false, dismissed: true);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldShow_WhenNudgeConditionsMetAndNotShownOrDismissed_IsTrue()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var result = SignInNudgePolicy.ShouldShow(snapshot, shownThisSession: false, dismissed: false);

            Assert.IsTrue(result);
        }
    }
}
