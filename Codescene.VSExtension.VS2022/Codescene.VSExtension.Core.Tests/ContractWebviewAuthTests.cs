// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Application.Refactoring;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class ContractWebviewAuthTests
    {
        public static readonly string[] AuthMessageSamples = new[]
        {
            WebComponentConstants.MessageTypes.OPENAUTH,
            WebComponentConstants.MessageTypes.AUTHCLOSE,
            WebComponentConstants.MessageTypes.AUTHSKIPPROJECT,
            WebComponentConstants.MessageTypes.HIDESIGNINBANNER,
            WebComponentConstants.MessageTypes.AUTHSIGNIN,
            WebComponentConstants.MessageTypes.AUTHOPENBROWSERAGAIN,
            WebComponentConstants.MessageTypes.AUTHSIGNOUT,
            WebComponentConstants.MessageTypes.AUTHRETRY,
            WebComponentConstants.MessageTypes.AUTHSELECTACCOUNT,
            WebComponentConstants.MessageTypes.AUTHLINKPROJECT,
            WebComponentConstants.MessageTypes.AUTHSETACCESSTOKEN,
            WebComponentConstants.MessageTypes.AUTHREMOVEACCESSTOKEN,
            WebComponentConstants.MessageTypes.AUTHOPENSERVERSETTING,
            WebComponentConstants.MessageTypes.AUTHOPENPATPAGE,
            WebComponentConstants.MessageTypes.AUTHREAUTHORIZE,
        };

        private static ContractBundle _bundle = null!;
        private static CwfSchemas _cwf = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
            _cwf = CwfSchemas.Locate();
        }

        [TestCleanup]
        public void Cleanup()
        {
            WebComponentJson.AuthSnapshotSource = null;
        }

        [TestMethod]
        [DataRow("env-token-override")]
        [DataRow("full")]
        [DataRow("minimal")]
        [DataRow("signed-in-oauth-setting-token-rejected")]
        [DataRow("signed-in-reauthorize-needed")]
        [DataRow("signed-in-setting-token-onprem")]
        [DataRow("signed-in-with-error")]
        [DataRow("signed-in")]
        [DataRow("signed-out-env-token-invalid")]
        [DataRow("signed-out-oauth-unsupported")]
        [DataRow("signed-out-timeout")]
        [DataRow("signed-out-with-error")]
        [DataRow("signed-out")]
        [DataRow("signing-in-with-error")]
        [DataRow("signing-in")]
        public void HomePayload_WhileActive_ValidatesAndIsClosed(string statusVariant)
        {
            var status = (JObject)_bundle.Fixture("auth-status", statusVariant, FixtureForm.Wire);
            var accounts = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);
            var projects = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);
            var link = (JObject)_bundle.Fixture("project-link", "remote", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithAccounts(accounts)
                .WithProjects(projects)
                .WithRepo(new AuthRepo("C:\\repo", "repo"))
                .WithLinkedProject(link["project"] as JObject)
                .WithProjectSource(link["source"]?.Value<string>())
                .WithStep(AuthConstants.Steps.SUMMARY)
                .WithContext(AuthConstants.Contexts.MANUAL);

            var payload = HomePayload(snapshot);

            AssertValidAndClosed(payload, copy => (JObject)At(copy, "auth"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "auth.status"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "authFlow"));
        }

        [TestMethod]
        [DataRow(AuthConstants.Steps.SUMMARY, AuthConstants.Contexts.SIGNIN)]
        [DataRow(AuthConstants.Steps.SUMMARY, AuthConstants.Contexts.MANUAL)]
        [DataRow(AuthConstants.Steps.RESOLVING, AuthConstants.Contexts.SIGNIN)]
        [DataRow(AuthConstants.Steps.PICKACCOUNT, AuthConstants.Contexts.SIGNIN)]
        [DataRow(AuthConstants.Steps.PICKACCOUNT, AuthConstants.Contexts.MANUAL)]
        [DataRow(AuthConstants.Steps.PICKPROJECT, AuthConstants.Contexts.SIGNIN)]
        [DataRow(AuthConstants.Steps.PICKPROJECT, AuthConstants.Contexts.MANUAL)]
        public void AuthViewPayload_WhileActive_ValidatesAndIsClosed(string step, string context)
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithSurface(AuthConstants.Surfaces.AUTH)
                .WithStep(step)
                .WithContext(context);

            var payload = AuthViewPayload(snapshot);

            AssertValidAndClosed(payload, copy => (JObject)At(copy, "auth"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "authFlow"));
        }

        [TestMethod]
        public void DocsPayload_WhileActive_ValidatesAndIsClosed()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status);

            var payload = DocsPayload(snapshot);

            AssertValidAndClosed(payload, copy => (JObject)At(copy, "auth"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "authFlow"));
        }

        [TestMethod]
        [DataRow("home")]
        [DataRow("auth")]
        public void UpdateRendererMessage_WhileActive_CarriesAValidPayload(string view)
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithSurface(view == "auth" ? AuthConstants.Surfaces.AUTH : AuthConstants.Surfaces.MONITOR);
            var message = new WebComponentMessage<CodeHealthMonitorComponentData>
            {
                MessageType = WebComponentConstants.MessageTypes.UPDATERENDERER,
                Payload = HomePayloadObject(snapshot),
            };

            var sent = Serialize(message, snapshot);

            var parsed = JObject.Parse(sent);
            var payload = (JObject)At(parsed, "payload");
            _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(payload));
        }

        [TestMethod]
        public void Payload_WhileActive_WithAceCredential_WouldFail()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "full", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status);

            var payload = HomePayload(snapshot);
            var auth = (JObject)At(payload, "auth");
            var authStatus = (JObject)At(auth, "status");

            Assert.IsFalse(authStatus.ContainsKey("ace-credential"), "ace-credential should be stripped");
            Assert.IsTrue(status.ContainsKey("ace-credential"), "fixture should have ace-credential");

            var withAceCredential = (JObject)payload.DeepClone();
            ((JObject)At(withAceCredential, "auth.status"))["ace-credential"] = false;

            AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(withAceCredential)), "ace-credential");
        }

        [TestMethod]
        public void Payload_WithBannerHidden_Validates()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithSignInBannerHidden(true);

            var payload = HomePayload(snapshot);

            Assert.AreEqual(true, payload["signInBannerHidden"]?.Value<bool>());
            _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(payload));
        }

        [TestMethod]
        public void Payload_WithNotLoadedAccounts_Validates()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithAccounts(null)
                .WithProjects(null);

            var payload = HomePayload(snapshot);

            var auth = (JObject)At(payload, "auth");
            Assert.IsNull(auth["accounts"].Value<JArray>());
            Assert.IsNull(auth["projects"].Value<JArray>());
            _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(payload));
        }

        [TestMethod]
        [DataRow(@"{""messageType"":""open-auth""}")]
        [DataRow(@"{""messageType"":""open-auth"",""payload"":{""screen"":""account""}}")]
        [DataRow(@"{""messageType"":""open-auth"",""payload"":{""screen"":""project""}}")]
        [DataRow(@"{""messageType"":""auth-close""}")]
        [DataRow(@"{""messageType"":""auth-skip-project""}")]
        [DataRow(@"{""messageType"":""hide-sign-in-banner""}")]
        [DataRow(@"{""messageType"":""auth-sign-in""}")]
        [DataRow(@"{""messageType"":""auth-open-browser-again""}")]
        [DataRow(@"{""messageType"":""auth-sign-out""}")]
        [DataRow(@"{""messageType"":""auth-retry""}")]
        [DataRow(@"{""messageType"":""auth-select-account"",""payload"":{""accountId"":""42""}}")]
        [DataRow(@"{""messageType"":""auth-link-project"",""payload"":{""projectId"":""proj-123""}}")]
        [DataRow(@"{""messageType"":""auth-link-project"",""payload"":{""projectId"":null}}")]
        [DataRow(@"{""messageType"":""auth-set-access-token"",""payload"":{""token"":""fake-token""}}")]
        [DataRow(@"{""messageType"":""auth-remove-access-token""}")]
        [DataRow(@"{""messageType"":""auth-open-server-setting""}")]
        [DataRow(@"{""messageType"":""auth-open-pat-page""}")]
        [DataRow(@"{""messageType"":""auth-reauthorize""}")]
        [DataRow(@"{""messageType"":""init"",""payload"":""auth""}")]
        public void AuthMessage_ValidatesAndIsReadWithoutLosingFields(string sample)
        {
            var message = JObject.Parse(sample);

            _cwf.ValidateMessage(message);

            var topLevel = (JObject)message.DeepClone();
            topLevel["undeclared"] = true;
            AssertFails(() => _cwf.ValidateMessage(topLevel), "undeclared");

            if (message["payload"] is JObject)
            {
                var nested = (JObject)message.DeepClone();
                ((JObject)At(nested, "payload"))["undeclared"] = true;
                AssertFails(() => _cwf.ValidateMessage(nested), "undeclared");
            }
        }

        [TestMethod]
        public void AuthLinkProjectWithNull_IsReadAsResetToAutomatic()
        {
            var sample = @"{""messageType"":""auth-link-project"",""payload"":{""projectId"":null}}";
            var message = JObject.Parse(sample);

            _cwf.ValidateMessage(message);

            var payload = (JObject)message["payload"];
            Assert.AreEqual(JTokenType.Null, payload["projectId"].Type);
        }

        [TestMethod]
        public void AuthMessage_WithAnUndeclaredField_Fails()
        {
            var message = JObject.Parse(@"{""messageType"":""auth-sign-in"",""undeclared"":1}");

            AssertFails(() => _cwf.ValidateMessage(message), "undeclared");
        }

        private static JToken At(JToken root, string path)
        {
            return root.SelectToken(path) ?? throw new InvalidOperationException($"{path} is missing in {root.ToString(Formatting.None)}");
        }

        private static string Serialize(object value, AuthSnapshot snapshot)
        {
            return WebComponentJson.Serialize(value, snapshot);
        }

        private static JObject Sent(object value, AuthSnapshot snapshot)
        {
            return JObject.Parse(Serialize(value, snapshot));
        }

        private static WebComponentPayload<CodeHealthMonitorComponentData> HomePayloadObject(AuthSnapshot snapshot)
        {
            var availability = new Mock<IAgentAvailability>();
            availability.Setup(x => x.Enabled).Returns(true);
            availability.Setup(x => x.Authenticated).Returns(true);
            availability.Setup(x => x.AutoRefactor()).Returns(new AutoRefactorConfig { Activated = true, Visible = true, Disabled = false });
            var mapper = new CodeHealthMonitorMapper(availability.Object, new CodeHealthMonitorNotifier());
            return CodeHealthMonitorView.Payload(CodeHealthMonitorView.For(snapshot), mapper.Map(new Dictionary<string, DeltaResponseModel>()));
        }

        private static JObject HomePayload(AuthSnapshot snapshot)
        {
            return Sent(HomePayloadObject(snapshot), snapshot);
        }

        private static WebComponentPayload<CodeHealthMonitorComponentData> AuthViewPayloadObject(AuthSnapshot snapshot)
        {
            Assert.AreEqual(WebComponentConstants.ViewTypes.AUTH, CodeHealthMonitorView.For(snapshot));
            return HomePayloadObject(snapshot);
        }

        private static JObject AuthViewPayload(AuthSnapshot snapshot)
        {
            var sent = Sent(AuthViewPayloadObject(snapshot), snapshot);
            Assert.IsNull(sent["data"]);
            return sent;
        }

        private static JObject DocsPayload(AuthSnapshot snapshot)
        {
            var availability = new Mock<IAgentAvailability>();
            availability.Setup(x => x.Authenticated).Returns(true);
            availability.Setup(x => x.AutoRefactor()).Returns(new AutoRefactorConfig
            {
                Visible = true,
                Activated = true,
                Disabled = false,
            });
            var model = new ShowDocumentationModel("src/fake.js", "Complex Method", "fakeFunction", new CodeRangeModel(3, 9, 1, 2));
            var data = new CodeSmellDocumentationMapper(availability.Object).Map(model, null);
            return Sent(WebComponentPayload<CodeSmellDocumentationComponentData>.Create(WebComponentConstants.ViewTypes.DOCS, data), snapshot);
        }

        private static void AssertFails(Action validate, string expectedInMessage)
        {
            var failure = Assert.Throws<AssertFailedException>(validate);
            StringAssert.Contains(failure.Message, expectedInMessage);
        }

        private static void AssertValidAndClosed(JObject sent, Func<JObject, JObject> locate)
        {
            _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(sent));

            var topLevel = (JObject)sent.DeepClone();
            topLevel["undeclared"] = true;
            AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(topLevel)), "undeclared");

            var nested = (JObject)sent.DeepClone();
            locate(nested)["undeclared"] = true;
            AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(nested)), "undeclared");
        }
    }
}
