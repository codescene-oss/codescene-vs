// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthServiceAccountDataTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public async Task SignedIn_LoadsAccountsProjectsAndLinkInParallelOnce()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData("full");
            var accounts = new TaskCompletionSource<JArray>();
            var projects = new TaskCompletionSource<JArray>();
            var link = new TaskCompletionSource<JObject>();
            kit.AuthClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>())).Returns(accounts.Task);
            kit.AuthClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>())).Returns(projects.Task);
            kit.AuthClient.Setup(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>())).Returns(link.Task);
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));

            kit.Start("signed-in");

            kit.AuthClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);
            kit.AuthClient.Verify(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()), Times.Once);
            kit.AuthClient.Verify(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>()), Times.Once);
            Assert.IsTrue(kit.Service.Current.Syncing);

            accounts.SetResult((JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire));
            projects.SetResult((JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire));
            link.SetResult(kit.Link("full"));
            await kit.IdleAsync();

            kit.AuthClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);
            kit.AuthClient.Verify(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>()), Times.Once);
            Assert.IsNotNull(kit.Service.Current.Accounts);
            Assert.IsNotNull(kit.Service.Current.Projects);
            Assert.AreEqual("101", (string)kit.Service.Current.LinkedProject["id"]);
            Assert.IsFalse(kit.Service.Current.Syncing);
        }

        [TestMethod]
        public void SameKeyStatus_DoesNotReload()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var scheduler = new InlineAsyncTaskScheduler();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(true);
            settings.Setup(x => x.ServerUrl).Returns("https://example.com");
            settings.Setup(x => x.AccessToken).Returns("token");

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var accounts = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);
            var projects = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, status);

            authClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);
            authClient.Verify(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void AccountChange_Reloads()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var scheduler = new InlineAsyncTaskScheduler();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(true);
            settings.Setup(x => x.ServerUrl).Returns("https://example.com");
            settings.Setup(x => x.AccessToken).Returns("token");

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status1 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var status2 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            status2["account"]["id"] = "different-account";

            var accounts = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);
            var projects = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status1);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, status2);

            authClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            authClient.Verify(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public void FailingListRequest_LeavesThatFieldNullAndLogsTheCode()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var scheduler = new InlineAsyncTaskScheduler();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(true);
            settings.Setup(x => x.ServerUrl).Returns("https://example.com");
            settings.Setup(x => x.AccessToken).Returns("token");

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var accounts = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DevtoolsException("test-error", -32803, "trace", "auth/timeout"));

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            Assert.IsNotNull(service.Current.Accounts);
            Assert.IsNull(service.Current.Projects);
            Assert.IsTrue(logger.Warnings.Exists(m => m.Contains("[auth] account data request failed code=auth/timeout")));
        }

        [TestMethod]
        public async Task LinkedFromRemote_SetsLinkedProjectAndSource()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData("remote");
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));

            kit.Start("signed-in");
            await kit.IdleAsync();

            Assert.AreEqual("101", (string)kit.Service.Current.LinkedProject["id"]);
            Assert.AreEqual("remote", kit.Service.Current.ProjectSource);
            Assert.IsNull(kit.Service.Current.ProjectReason);
            Assert.AreEqual(0, kit.Service.Current.ProjectCandidates.Count);
        }

        [TestMethod]
        [DataRow("no-project-candidates", "other-account", 2)]
        [DataRow("lookup-failed", "lookup-failed", 0)]
        public async Task NotLinked_SetsReasonAndCandidates(string variant, string reason, int candidates)
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData(variant);
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));

            kit.Start("signed-in");
            await kit.IdleAsync();

            Assert.IsNull(kit.Service.Current.LinkedProject);
            Assert.IsNull(kit.Service.Current.ProjectSource);
            Assert.AreEqual(reason, kit.Service.Current.ProjectReason);
            Assert.AreEqual(candidates, kit.Service.Current.ProjectCandidates.Count);
        }

        [TestMethod]
        public void StaleAccountLoad_IsDropped()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(true);
            settings.Setup(x => x.ServerUrl).Returns("https://example.com");
            settings.Setup(x => x.AccessToken).Returns("token");

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status1 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var status2 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            status2["account"]["id"] = "different-account";

            var accounts1 = new JArray { new JObject { ["id"] = "account-1" } };
            var accounts2 = new JArray { new JObject { ["id"] = "account-2" } };
            var projects = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);

            var tcs1 = new TaskCompletionSource<JArray>();
            var tcs2 = new TaskCompletionSource<JArray>();
            var calls = 0;

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status1);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    calls++;
                    return calls == 1 ? tcs1.Task : tcs2.Task;
                });
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            var scheduledTasks = new List<Func<Task>>();
            var runningTasks = new List<Task>();
            var scheduler = new Mock<IAsyncTaskScheduler>();
            scheduler.Setup(x => x.Schedule(It.IsAny<Func<Task>>()))
                .Callback<Func<Task>>(work =>
                {
                    scheduledTasks.Add(work);
                    var task = Task.Run(() => work());
                    lock (runningTasks)
                    {
                        runningTasks.Add(task);
                    }
                });

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler.Object, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });
            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, status2);

            tcs2.SetResult(accounts2);
            tcs1.SetResult(accounts1);

            Task[] tasksToWait;
            lock (runningTasks)
            {
                tasksToWait = runningTasks.ToArray();
            }

            Task.WaitAll(tasksToWait, TimeSpan.FromSeconds(5));

            var accountId = service.Current.Accounts?[0]?["id"]?.Value<string>();
            Assert.AreEqual("account-2", accountId);
        }

        [TestMethod]
        public void Syncing_IsTrueWhileLoadingOrResolving()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var scheduler = new InlineAsyncTaskScheduler();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(true);
            settings.Setup(x => x.ServerUrl).Returns("https://example.com");
            settings.Setup(x => x.AccessToken).Returns("token");

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var signingInStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var signedInStatus = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var accounts = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);
            var projects = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);

            var tcsAccounts = new TaskCompletionSource<JArray>();
            var tcsProjects = new TaskCompletionSource<JArray>();
            var tcsSync = new TaskCompletionSource<JObject>();

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signingInStatus);
            authClient.Setup(x => x.AuthSyncAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(tcsSync.Task);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .Returns(tcsAccounts.Task);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .Returns(tcsProjects.Task);

            var snapshots = new List<bool>();
            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);
            service.SnapshotChanged += (_, s) => snapshots.Add(s.Syncing);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, signedInStatus);

            Assert.IsTrue(service.Current.Syncing);

            tcsSync.SetResult(signedInStatus);
            tcsAccounts.SetResult(accounts);
            tcsProjects.SetResult(projects);

            Assert.IsFalse(service.Current.Syncing);
            Assert.IsTrue(snapshots.Any(s => s));
        }
    }
}
