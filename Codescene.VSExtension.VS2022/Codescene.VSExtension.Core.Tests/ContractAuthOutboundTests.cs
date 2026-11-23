// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ContractAuthOutboundTests
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
        [DataRow("auth-configure-params", "full,null-values", "minimal")]
        [DataRow("cancel-login-params", "full,minimal", "")]
        [DataRow("select-account-params", "full,minimal,integer-account-id", "")]
        [DataRow("repo-path-params", "full,minimal", "")]
        [DataRow("project-link-set-params", "full,minimal,integer-project-id", "")]
        public void SentVariants_AreTheVariantsTheBundleShips(string root, string sent, string notSent)
        {
            var expected = (sent + "," + notSent).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            CollectionAssert.AreEquivalent(expected, _bundle.FixtureVariants(root).ToArray());
        }

        [TestMethod]
        public void EmptyParams_ShipsAtLeastOneVariant()
        {
            Assert.IsTrue(_bundle.FixtureVariants("empty-params").Any());
        }

        [TestMethod]
        [DataRow("full")]
        [DataRow("null-values")]
        public async Task AuthConfigure_SentByTheClient_ValidatesWithKebabKeys(string variant)
        {
            var fixture = (JObject)_bundle.Fixture("auth-configure-params", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.AuthConfigureAsync(
                (string?)fixture["server-url"],
                (string?)fixture["access-token"]);
            var sent = await WaitForAsync(server.AuthConfigure.Task);

            AssertSent("auth-configure-params", fixture, sent);
        }

        [TestMethod]
        public async Task SelectAccount_WithAnIntegerId_SendsItUnchanged()
        {
            var fixture = (JObject)_bundle.Fixture("select-account-params", "integer-account-id", FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.AuthSelectAccountAsync(fixture["account-id"] ?? throw new InvalidOperationException("fixture has no account-id"));
            var sent = await WaitForAsync(server.AuthSelectAccount.Task);

            AssertSent("select-account-params", fixture, sent);
        }

        [TestMethod]
        public async Task ProjectLinkSet_WithAnIntegerId_SendsItUnchanged()
        {
            var fixture = (JObject)_bundle.Fixture("project-link-set-params", "integer-project-id", FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.ProjectLinkSetAsync(
                Text(fixture, "repo-path"),
                fixture["project-id"] ?? throw new InvalidOperationException("fixture has no project-id"));
            var sent = await WaitForAsync(server.ProjectLinkSet.Task);

            AssertSent("project-link-set-params", fixture, sent);
        }

        [TestMethod]
        public async Task ProjectLinkSet_WithNull_SendsAnExplicitNull()
        {
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.ProjectLinkSetAsync("/fake/repo", null);
            var sent = await WaitForAsync(server.ProjectLinkSet.Task);

            Assert.AreEqual(JTokenType.Null, sent["project-id"]?.Type, "project-id must be an explicit null");
        }

        [TestMethod]
        public async Task CancelLogin_WithLoginId_SendsTheId()
        {
            var fixture = (JObject)_bundle.Fixture("cancel-login-params", "full", FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.AuthCancelLoginAsync(Text(fixture, "login-id"));
            var sent = await WaitForAsync(server.AuthCancelLogin.Task);

            AssertSent("cancel-login-params", fixture, sent);
        }

        [TestMethod]
        public async Task SyncAccount_WithRepoPath_SendsThePath()
        {
            var fixture = (JObject)_bundle.Fixture("repo-path-params", "full", FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.AuthSyncAccountAsync(Text(fixture, "repo-path"));
            var sent = await WaitForAsync(server.AuthSyncAccount.Task);

            AssertSent("repo-path-params", fixture, sent);
        }

        [TestMethod]
        public async Task ProjectLinkGet_WithRepoPath_SendsThePath()
        {
            var fixture = (JObject)_bundle.Fixture("repo-path-params", "full", FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.ProjectLinkGetAsync(Text(fixture, "repo-path"));
            var sent = await WaitForAsync(server.ProjectLinkGet.Task);

            AssertSent("repo-path-params", fixture, sent);
        }

        [TestMethod]
        public async Task AuthToken_SendsEmptyParams()
        {
            var fixture = (JObject)_bundle.Fixture("empty-params", _bundle.FixtureVariants("empty-params").First(), FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            await harness.Client.AuthTokenAsync();
            var sent = await WaitForAsync(server.AuthToken.Task);

            AssertSent("empty-params", fixture, sent);
        }

        [TestMethod]
        [DataRow("cs-ide/auth-status")]
        [DataRow("cs-ide/auth-login")]
        [DataRow("cs-ide/auth-reauthorize")]
        [DataRow("cs-ide/auth-logout")]
        [DataRow("cs-ide/auth-list-accounts")]
        [DataRow("cs-ide/projects-list")]
        public async Task EmptyParamsRequests_SendAnEmptyObject(string method)
        {
            var (server, harness) = await StartHarnessAsync();

            switch (method)
            {
                case "cs-ide/auth-status":
                    await harness.Client.AuthStatusAsync();
                    var statusSent = await WaitForAsync(server.AuthStatus.Task);
                    AssertEmpty(statusSent);
                    break;
                case "cs-ide/auth-login":
                    await harness.Client.AuthLoginAsync();
                    var loginSent = await WaitForAsync(server.AuthLogin.Task);
                    AssertEmpty(loginSent);
                    break;
                case "cs-ide/auth-reauthorize":
                    await harness.Client.AuthReauthorizeAsync();
                    var reauthorizeSent = await WaitForAsync(server.AuthReauthorize.Task);
                    AssertEmpty(reauthorizeSent);
                    break;
                case "cs-ide/auth-logout":
                    await harness.Client.AuthLogoutAsync();
                    var logoutSent = await WaitForAsync(server.AuthLogout.Task);
                    AssertEmpty(logoutSent);
                    break;
                case "cs-ide/auth-list-accounts":
                    await harness.Client.AuthListAccountsAsync();
                    var listAccountsSent = await WaitForAsync(server.AuthListAccounts.Task);
                    AssertEmpty(listAccountsSent);
                    break;
                case "cs-ide/projects-list":
                    await harness.Client.ProjectsListAsync();
                    var projectsListSent = await WaitForAsync(server.ProjectsList.Task);
                    AssertEmpty(projectsListSent);
                    break;
            }
        }

        private static void AssertEmpty(JObject sent)
        {
            Assert.AreEqual(0, sent.Properties().Count(), "Empty params must be an empty object");
        }

        private static string Text(JToken parent, string key)
        {
            return (string?)parent[key] ?? throw new InvalidOperationException($"{key} is null or missing");
        }

        private static void AssertSent(string root, JObject expected, JToken sent)
        {
            var (schemaFile, definition) = _bundle.FixtureSchema(root);
            ContractBundle.AssertCasing(sent, Casing.Kebab);
            ContractBundle.AssertDeclaredFieldsMatch(expected, sent, ContractEntityForm.FieldPaths(expected, sent));
            _bundle.Validate(schemaFile, definition, sent);
        }

        private static async Task<T> WaitForAsync<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != task)
            {
                throw new TimeoutException("Timed out waiting for the message to reach the server.");
            }

            return await task;
        }

        private async Task<(CapturingServer Server, ContractClientHarness Harness)> StartHarnessAsync()
        {
            var startWire = (JObject)_bundle.Fixture("start-notification", "minimal", FixtureForm.Wire);
            var server = new CapturingServer();
            var harness = ContractClientHarness.Create(startWire, server);
            _harnesses.Add(harness);
            await harness.Client.StartAsync();
            return (server, harness);
        }

        private sealed class CapturingServer
        {
            public TaskCompletionSource<JObject> AuthConfigure { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthStatus { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthLogin { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthReauthorize { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthLogout { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthCancelLogin { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthListAccounts { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthSelectAccount { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthSyncAccount { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> AuthToken { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> ProjectsList { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> ProjectLinkGet { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> ProjectLinkSet { get; } = new TaskCompletionSource<JObject>();

            [JsonRpcMethod("cs-ide/auth-configure", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthConfigure(JObject parameters)
            {
                AuthConfigure.TrySetResult(parameters);
                return new JObject { ["state"] = "signed-out" };
            }

            [JsonRpcMethod("cs-ide/auth-status", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthStatus(JObject parameters)
            {
                AuthStatus.TrySetResult(parameters);
                return new JObject { ["state"] = "signed-out" };
            }

            [JsonRpcMethod("cs-ide/auth-login", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthLogin(JObject parameters)
            {
                AuthLogin.TrySetResult(parameters);
                return new JObject { ["state"] = "signing-in" };
            }

            [JsonRpcMethod("cs-ide/auth-reauthorize", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthReauthorize(JObject parameters)
            {
                AuthReauthorize.TrySetResult(parameters);
                return new JObject { ["state"] = "signing-in" };
            }

            [JsonRpcMethod("cs-ide/auth-logout", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthLogout(JObject parameters)
            {
                AuthLogout.TrySetResult(parameters);
                return new JObject { ["state"] = "signed-out" };
            }

            [JsonRpcMethod("cs-ide/auth-cancel-login", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthCancelLogin(JObject parameters)
            {
                AuthCancelLogin.TrySetResult(parameters);
                return new JObject { ["state"] = "signed-out" };
            }

            [JsonRpcMethod("cs-ide/auth-list-accounts", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthListAccounts(JObject parameters)
            {
                AuthListAccounts.TrySetResult(parameters);
                return new JArray();
            }

            [JsonRpcMethod("cs-ide/auth-select-account", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthSelectAccount(JObject parameters)
            {
                AuthSelectAccount.TrySetResult(parameters);
                return new JObject { ["state"] = "signed-in" };
            }

            [JsonRpcMethod("cs-ide/auth-sync-account", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthSyncAccount(JObject parameters)
            {
                AuthSyncAccount.TrySetResult(parameters);
                return new JObject { ["state"] = "signed-in" };
            }

            [JsonRpcMethod("cs-ide/auth-token", UseSingleObjectParameterDeserialization = true)]
            public object? OnAuthToken(JObject parameters)
            {
                AuthToken.TrySetResult(parameters);
                return new JObject { ["credential-source"] = "env-token", ["access-token"] = "fake-token" };
            }

            [JsonRpcMethod("cs-ide/projects-list", UseSingleObjectParameterDeserialization = true)]
            public object? OnProjectsList(JObject parameters)
            {
                ProjectsList.TrySetResult(parameters);
                return new JArray();
            }

            [JsonRpcMethod("cs-ide/project-link-get", UseSingleObjectParameterDeserialization = true)]
            public object? OnProjectLinkGet(JObject parameters)
            {
                ProjectLinkGet.TrySetResult(parameters);
                return new JObject { ["project"] = JValue.CreateNull(), ["reason"] = "no-remote" };
            }

            [JsonRpcMethod("cs-ide/project-link-set", UseSingleObjectParameterDeserialization = true)]
            public object? OnProjectLinkSet(JObject parameters)
            {
                ProjectLinkSet.TrySetResult(parameters);
                return new JObject { ["project"] = parameters["project-id"], ["source"] = "link" };
            }
        }
    }
}
