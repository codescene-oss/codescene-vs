// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthWebviewFieldsTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void ToAuthPayload_WhenInactiveOrWithoutStatus_IsNull()
        {
            var inactive = AuthSnapshot.Inactive;
            var activeWithoutStatus = inactive.WithActive(true);

            Assert.IsNull(AuthWebviewFields.ToAuthPayload(inactive));
            Assert.IsNull(AuthWebviewFields.ToAuthPayload(activeWithoutStatus));
        }

        [TestMethod]
        public void ToAuthPayload_KeepsNulls()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "full", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithAccounts(null)
                .WithProjects(null)
                .WithRepo(null)
                .WithLinkedProject(null);

            var result = AuthWebviewFields.ToAuthPayload(snapshot);

            Assert.IsNotNull(result);
            Assert.IsNull(result["accounts"].Value<JArray>());
            Assert.IsNull(result["projects"].Value<JArray>());
            Assert.IsNull(result["repo"].Value<JObject>());
            Assert.IsNull(result["linked-project"].Value<JObject>());
        }

        [TestMethod]
        public void ToAuthFlow_HasKebabKeysAndNulls()
        {
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStep(AuthConstants.Steps.PICKPROJECT)
                .WithContext(AuthConstants.Contexts.SIGNIN)
                .WithProjectSource(null)
                .WithSyncing(true)
                .WithProjectReason("no-remote")
                .WithProjectCandidates(new JArray());

            var result = AuthWebviewFields.ToAuthFlow(snapshot);

            Assert.IsNotNull(result);
            Assert.AreEqual(AuthConstants.Steps.PICKPROJECT, result["step"].Value<string>());
            Assert.AreEqual(AuthConstants.Contexts.SIGNIN, result["context"].Value<string>());
            Assert.IsNull(result["project-source"].Value<string>());
            Assert.AreEqual(true, result["syncing"].Value<bool>());
            Assert.AreEqual("no-remote", result["project-reason"].Value<string>());
            Assert.IsNotNull(result["project-candidates"]);
        }

        [TestMethod]
        public void Inject_AddsIdeAuthOnceAndTheFields()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithStep(AuthConstants.Steps.SUMMARY)
                .WithContext(AuthConstants.Contexts.MANUAL);
            var payload = new JObject { ["view"] = "home" };

            AuthWebviewFields.Inject(payload, snapshot);

            var featureFlags = (JArray)payload["featureFlags"];
            Assert.AreEqual(1, featureFlags.Count);
            Assert.AreEqual("ide-auth", featureFlags[0].Value<string>());
            Assert.IsNotNull(payload["auth"]);
            Assert.IsNotNull(payload["authFlow"]);

            AuthWebviewFields.Inject(payload, snapshot);

            Assert.AreEqual(1, featureFlags.Count, "ide-auth should not be duplicated");
        }

        [TestMethod]
        public void Inject_OmitsSignInBannerHiddenUnlessHidden()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var shown = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithSignInBannerHidden(false);
            var hidden = shown.WithSignInBannerHidden(true);
            var payloadShown = new JObject();
            var payloadHidden = new JObject();

            AuthWebviewFields.Inject(payloadShown, shown);
            AuthWebviewFields.Inject(payloadHidden, hidden);

            Assert.IsFalse(payloadShown.ContainsKey("signInBannerHidden"));
            Assert.AreEqual(true, payloadHidden["signInBannerHidden"].Value<bool>());
        }

        [TestMethod]
        public void Serialize_WithoutASource_IsUnchanged()
        {
            WebComponentJson.AuthSnapshotSource = null;
            var data = new CodeHealthMonitorComponentData { AnalysisState = "idle" };

            var result = WebComponentJson.Serialize(data);

            Assert.IsFalse(result.Contains("ide-auth"));
            Assert.IsFalse(result.Contains("auth"));
        }

        [TestMethod]
        public void Serialize_WithAnInactiveSnapshot_IsUnchanged()
        {
            WebComponentJson.AuthSnapshotSource = () => AuthSnapshot.Inactive;
            var data = new CodeHealthMonitorComponentData { AnalysisState = "idle" };

            var result = WebComponentJson.Serialize(data);

            Assert.IsFalse(result.Contains("ide-auth"));
            Assert.IsFalse(result.Contains("auth"));
        }

        [TestMethod]
        public void Serialize_UpdateRendererMessage_InjectsIntoThePayload()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status);
            var message = new JObject
            {
                ["messageType"] = "update-renderer",
                ["payload"] = new JObject { ["view"] = "home" },
            };

            var result = WebComponentJson.Serialize(message, snapshot);

            var parsed = JObject.Parse(result);
            var payload = (JObject)parsed["payload"];
            Assert.IsNotNull(payload["auth"]);
            Assert.IsNotNull(payload["authFlow"]);
            var featureFlags = (JArray)payload["featureFlags"];
            Assert.IsTrue(featureFlags.Any(flag => flag.Value<string>() == "ide-auth"));
        }

        [TestMethod]
        public void CodeHealthMonitorView_For_Inactive()
        {
            var snapshot = AuthSnapshot.Inactive;

            var view = CodeHealthMonitorView.For(snapshot);

            Assert.AreEqual(WebComponentConstants.ViewTypes.HOME, view);
        }

        [TestMethod]
        public void CodeHealthMonitorView_For_ActiveMonitor()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithSurface(AuthConstants.Surfaces.MONITOR);

            var view = CodeHealthMonitorView.For(snapshot);

            Assert.AreEqual(WebComponentConstants.ViewTypes.HOME, view);
        }

        [TestMethod]
        public void CodeHealthMonitorView_For_ActiveAuth()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithSurface(AuthConstants.Surfaces.AUTH);

            var view = CodeHealthMonitorView.For(snapshot);

            Assert.AreEqual(WebComponentConstants.ViewTypes.AUTH, view);
        }

        [TestMethod]
        public void CodeHealthMonitorView_For_ActiveWithoutStatus()
        {
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithSurface(AuthConstants.Surfaces.AUTH);

            var view = CodeHealthMonitorView.For(snapshot);

            Assert.AreEqual(WebComponentConstants.ViewTypes.HOME, view);
        }
    }
}
