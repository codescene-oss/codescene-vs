// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ContractAuthInboundTests
    {
        private static ContractBundle _bundle = null!;

        private readonly List<ContractClientHarness> _harnesses = new List<ContractClientHarness>();

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var harness in _harnesses)
            {
                harness.Dispose();
            }
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
        public async Task AuthStatus_OverTheClient_KeepsTheFixture(string variant)
        {
            var wire = (JObject)_bundle.Fixture("auth-status", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();
            server.SetAuthStatusResponse(wire);

            var result = await harness.Client.AuthStatusAsync();

            Assert.IsTrue(JToken.DeepEquals(wire, result), $"Returned JObject does not deep-equal the wire fixture for variant {variant}");
        }

        [TestMethod]
        [DataRow("full")]
        [DataRow("minimal")]
        public async Task LoginPending_OverTheClient_KeepsTheFixture(string variant)
        {
            var wire = (JObject)_bundle.Fixture("login-pending", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();
            server.SetAuthLoginResponse(wire);

            var result = await harness.Client.AuthLoginAsync();

            Assert.IsTrue(JToken.DeepEquals(wire, result), $"Returned JObject does not deep-equal the wire fixture for variant {variant}");
        }

        [TestMethod]
        public async Task AccountList_OverTheClient_KeepsTheFixture()
        {
            var wire = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();
            server.SetAccountListResponse(wire);

            var result = await harness.Client.AuthListAccountsAsync();

            Assert.IsTrue(JToken.DeepEquals(wire, result), "Returned JArray does not deep-equal the wire fixture");
        }

        [TestMethod]
        public async Task ProjectList_OverTheClient_KeepsTheFixture()
        {
            var wire = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();
            server.SetProjectsListResponse(wire);

            var result = await harness.Client.ProjectsListAsync();

            Assert.IsTrue(JToken.DeepEquals(wire, result), "Returned JArray does not deep-equal the wire fixture");
        }

        [TestMethod]
        [DataRow("full")]
        [DataRow("lookup-failed")]
        [DataRow("minimal")]
        [DataRow("no-project-candidates")]
        [DataRow("remote")]
        public async Task ProjectLink_OverTheClient_KeepsTheFixture(string variant)
        {
            var wire = (JObject)_bundle.Fixture("project-link", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();
            server.SetProjectLinkGetResponse(wire);

            var result = await harness.Client.ProjectLinkGetAsync("/fake/repo");

            Assert.IsTrue(JToken.DeepEquals(wire, result), $"Returned JObject does not deep-equal the wire fixture for variant {variant}");
        }

        [TestMethod]
        [DataRow("signed-in")]
        [DataRow("signing-in")]
        [DataRow("signed-out-with-error")]
        [DataRow("full")]
        public async Task AuthStatusChanged_OverTheClient_RaisesTheFixture(string variant)
        {
            var wire = (JObject)_bundle.Fixture("auth-status", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();
            JObject receivedPayload = null;
            var eventReceived = new TaskCompletionSource<bool>();
            harness.Client.AuthStatusChanged += (sender, payload) =>
            {
                receivedPayload = payload;
                eventReceived.TrySetResult(true);
            };

            await harness.ServerRpc.NotifyWithParameterObjectAsync("cs-ide/authStatusChanged", wire);

            var completed = await Task.WhenAny(eventReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(completed == eventReceived.Task, "Timed out waiting for AuthStatusChanged event");
            Assert.IsNotNull(receivedPayload, "Event payload should not be null");
            Assert.IsTrue(JToken.DeepEquals(wire, receivedPayload), $"Event payload does not deep-equal the wire fixture for variant {variant}");
            _bundle.Validate("auth.json", "AuthStatus", receivedPayload);
        }

        [TestMethod]
        public async Task AuthStatusChanged_WithoutParams_RaisesNothing()
        {
            var (server, harness) = await StartHarnessAsync();
            var eventFired = false;
            harness.Client.AuthStatusChanged += (sender, payload) =>
            {
                eventFired = true;
            };

            await harness.ServerRpc.NotifyWithParameterObjectAsync("cs-ide/authStatusChanged", null);

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.IsFalse(eventFired, "AuthStatusChanged event should not fire for null notification");
        }

        private static async Task<T> WaitForAsync<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != task)
            {
                throw new TimeoutException("Timed out waiting for the response.");
            }

            return await task;
        }

        private async Task<(ResponseProvidingServer Server, ContractClientHarness Harness)> StartHarnessAsync()
        {
            var startWire = (JObject)_bundle.Fixture("start-notification", "minimal", FixtureForm.Wire);
            var server = new ResponseProvidingServer();
            var harness = ContractClientHarness.Create(startWire, server);
            _harnesses.Add(harness);
            await harness.Client.StartAsync();
            return (server, harness);
        }

        private sealed class ResponseProvidingServer
        {
            private JObject _authStatusResponse = new JObject { ["state"] = "signed-out" };

            private JObject _authLoginResponse = new JObject { ["state"] = "signing-in" };

            private JArray _accountListResponse = new JArray();

            private JArray _projectsListResponse = new JArray();

            private JObject _projectLinkGetResponse = new JObject { ["project"] = JValue.CreateNull(), ["reason"] = "no-remote" };

            public void SetAuthStatusResponse(JObject response)
            {
                _authStatusResponse = response;
            }

            public void SetAuthLoginResponse(JObject response)
            {
                _authLoginResponse = response;
            }

            public void SetAccountListResponse(JArray response)
            {
                _accountListResponse = response;
            }

            public void SetProjectsListResponse(JArray response)
            {
                _projectsListResponse = response;
            }

            public void SetProjectLinkGetResponse(JObject response)
            {
                _projectLinkGetResponse = response;
            }

            [StreamJsonRpc.JsonRpcMethod("cs-ide/auth-status", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthStatus(JObject parameters)
            {
                return _authStatusResponse;
            }

            [StreamJsonRpc.JsonRpcMethod("cs-ide/auth-login", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthLogin(JObject parameters)
            {
                return _authLoginResponse;
            }

            [StreamJsonRpc.JsonRpcMethod("cs-ide/auth-list-accounts", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthListAccounts(JObject parameters)
            {
                return _accountListResponse;
            }

            [StreamJsonRpc.JsonRpcMethod("cs-ide/projects-list", UseSingleObjectParameterDeserialization = true)]
            public object? OnProjectsList(JObject parameters)
            {
                return _projectsListResponse;
            }

            [StreamJsonRpc.JsonRpcMethod("cs-ide/project-link-get", UseSingleObjectParameterDeserialization = true)]
            public object? OnProjectLinkGet(JObject parameters)
            {
                return _projectLinkGetResponse;
            }
        }
    }
}
