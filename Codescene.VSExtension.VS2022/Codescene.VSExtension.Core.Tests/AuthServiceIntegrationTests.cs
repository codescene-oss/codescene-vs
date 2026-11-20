// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class AuthServiceIntegrationTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public async Task SignInFlow_EndToEnd_OpensAuthAndReturnsToMonitor()
        {
            var server = new AuthServer("signed-out");
            using var run = await AuthRun.StartAsync(server);
            await run.UntilAsync(s => s.Active && s.Status != null);
            await run.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));

            await run.Service.SignInAsync();

            Assert.IsTrue(run.Snapshots.Any(s => s.Surface == AuthConstants.Surfaces.AUTH && s.Context == AuthConstants.Contexts.SIGNIN));
            CollectionAssert.AreEqual(new[] { (string)Fixture("login-pending", "full")["authorize-url"] }, run.OpenedUrls.ToArray());

            await run.NotifyStatusAsync("signing-in");
            await run.NotifyStatusAsync("signed-in");
            await run.UntilAsync(s => s.Step == AuthConstants.Steps.PICKPROJECT && !s.Syncing);

            Assert.AreEqual("/repo", (string)server.Params("cs-ide/auth-sync-account").Single()["repo-path"]);
            Assert.AreEqual("/repo", (string)server.Params("cs-ide/project-link-get").Single()["repo-path"]);
            Assert.AreEqual(1, server.Params("cs-ide/auth-list-accounts").Count);
            Assert.AreEqual(1, server.Params("cs-ide/projects-list").Count);
            Assert.AreEqual("other-account", run.Service.Current.ProjectReason);

            await run.Service.LinkProjectAsync(new JValue("101"));

            var set = server.Params("cs-ide/project-link-set").Single();
            Assert.AreEqual("/repo", (string)set["repo-path"]);
            Assert.AreEqual("101", (string)set["project-id"]);
            _bundle.Validate("auth.json", "ProjectLinkSetParams", set);
            Assert.AreEqual(AuthConstants.Surfaces.MONITOR, run.Service.Current.Surface);
            Assert.AreEqual(AuthConstants.Steps.SUMMARY, run.Service.Current.Step);
            Assert.AreEqual("link", run.Service.Current.ProjectSource);
            Assert.AreEqual("101", (string)run.Service.Current.LinkedProject["id"]);
        }

        [TestMethod]
        public async Task AuthSyncAccount_ReturningMethodNotFound_DisablesSyncForTheSession()
        {
            var server = new AuthServer("signed-in") { SyncMethodNotFound = true };
            using var run = await AuthRun.StartAsync(server);
            await run.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));
            await run.UntilAsync(s => s.Active && s.Accounts != null && !s.Syncing);

            await run.Service.SetRepoAsync(new AuthRepo("/repo2", "repo2"));
            await run.UntilAsync(s => !s.Syncing && s.ProjectReason != null);

            Assert.AreEqual(1, server.Params("cs-ide/auth-sync-account").Count);
            Assert.IsTrue(run.Logger.Warnings.Any(m => m.Contains("method=cs-ide/auth-sync-account") && m.Contains("code=-32601")));
            Assert.IsFalse(run.Logger.Warnings.Any(m => m.Contains("[auth] account sync failed")));
            Assert.AreEqual("/repo2", (string)server.Params("cs-ide/project-link-get").Last()["repo-path"]);
        }

        private static JObject Fixture(string root, string variant) => (JObject)_bundle.Fixture(root, variant, FixtureForm.Wire);

        private sealed class AuthRun : IDisposable
        {
            private readonly ContractClientHarness _harness;

            private AuthRun(ContractClientHarness harness, FakeLogger logger)
            {
                _harness = harness;
                Logger = logger;
                var settings = new Mock<IAuthSettings>();
                settings.SetupGet(x => x.BrowserSignInEnabled).Returns(true);
                var host = new Mock<IAuthHost>();
                host.Setup(x => x.OpenExternalAsync(It.IsAny<string>()))
                    .Callback<string>(url => OpenedUrls.Enqueue(url))
                    .Returns(Task.CompletedTask);
                host.Setup(x => x.RevealMonitorAsync()).Returns(Task.CompletedTask);
                Service = new AuthService(
                    harness.Client,
                    harness.Client,
                    settings.Object,
                    host.Object,
                    new Mock<ITelemetryManager>().Object,
                    new FakeDebounceService(),
                    new InlineAsyncTaskScheduler(),
                    logger);
                Service.SnapshotChanged += (sender, snapshot) => Snapshots.Enqueue(snapshot);
            }

            public AuthService Service { get; }

            public FakeLogger Logger { get; }

            public ConcurrentQueue<AuthSnapshot> Snapshots { get; } = new ConcurrentQueue<AuthSnapshot>();

            public ConcurrentQueue<string> OpenedUrls { get; } = new ConcurrentQueue<string>();

            public static async Task<AuthRun> StartAsync(AuthServer server)
            {
                var logger = new FakeLogger();
                var harness = ContractClientHarness.Create(Fixture("start-notification", "full"), server, logger);
                var run = new AuthRun(harness, logger);
                run.Service.Start();
                await harness.Client.StartAsync();
                return run;
            }

            public Task NotifyStatusAsync(string variant)
            {
                return _harness.ServerRpc.NotifyWithParameterObjectAsync("cs-ide/authStatusChanged", Fixture("auth-status", variant));
            }

            public async Task UntilAsync(Func<AuthSnapshot, bool> condition)
            {
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!condition(Service.Current))
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        Assert.Fail("The auth snapshot never reached the expected state");
                    }

                    await Task.Delay(10);
                }
            }

            public void Dispose() => _harness.Dispose();
        }

        private sealed class AuthServer
        {
            private readonly string _configureVariant;
            private readonly ConcurrentQueue<(string Method, JObject Params)> _received = new ConcurrentQueue<(string, JObject)>();

            public AuthServer(string configureVariant)
            {
                _configureVariant = configureVariant;
            }

            public bool SyncMethodNotFound { get; set; }

            public List<JObject> Params(string method) => _received.Where(r => r.Method == method).Select(r => r.Params).ToList();

            [JsonRpcMethod("cs-ide/auth-configure", UseSingleObjectParameterDeserialization = true)]
            public JObject AuthConfigure(JObject parameters) => Reply("cs-ide/auth-configure", parameters, Fixture("auth-status", _configureVariant));

            [JsonRpcMethod("cs-ide/auth-login", UseSingleObjectParameterDeserialization = true)]
            public JObject AuthLogin(JObject parameters) => Reply("cs-ide/auth-login", parameters, Fixture("login-pending", "full"));

            [JsonRpcMethod("cs-ide/auth-sync-account", UseSingleObjectParameterDeserialization = true)]
            public JObject AuthSyncAccount(JObject parameters)
            {
                Reply<JObject>("cs-ide/auth-sync-account", parameters, null);
                if (SyncMethodNotFound)
                {
                    throw new LocalRpcException("Method not found") { ErrorCode = -32601 };
                }

                return Fixture("auth-status", "signed-in");
            }

            [JsonRpcMethod("cs-ide/auth-list-accounts", UseSingleObjectParameterDeserialization = true)]
            public JArray AuthListAccounts(JObject parameters) => Reply("cs-ide/auth-list-accounts", parameters, (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire));

            [JsonRpcMethod("cs-ide/projects-list", UseSingleObjectParameterDeserialization = true)]
            public JArray ProjectsList(JObject parameters) => Reply("cs-ide/projects-list", parameters, (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire));

            [JsonRpcMethod("cs-ide/project-link-get", UseSingleObjectParameterDeserialization = true)]
            public JObject ProjectLinkGet(JObject parameters) => Reply("cs-ide/project-link-get", parameters, Fixture("project-link", "no-project-candidates"));

            [JsonRpcMethod("cs-ide/project-link-set", UseSingleObjectParameterDeserialization = true)]
            public JObject ProjectLinkSet(JObject parameters)
            {
                var link = Fixture("project-link", "remote");
                link["source"] = "link";
                return Reply("cs-ide/project-link-set", parameters, link);
            }

            private T Reply<T>(string method, JObject parameters, T result)
            {
                _received.Enqueue((method, parameters));
                return result;
            }
        }
    }
}
