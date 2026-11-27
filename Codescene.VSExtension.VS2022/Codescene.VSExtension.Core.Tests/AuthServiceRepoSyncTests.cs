// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
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
    public class AuthServiceRepoSyncTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public async Task SignInCompleted_SyncsAccountForTheRepoThenResolves()
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
            var link = (JObject)_bundle.Fixture("project-link", "full", FixtureForm.Wire);
            var repo = new AuthRepo("/repo", "repo");

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signingInStatus);
            authClient.Setup(x => x.AuthSyncAccountAsync("/repo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(signedInStatus);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);
            authClient.Setup(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(link);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            await service.SetRepoAsync(repo).ConfigureAwait(false);

            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, signedInStatus);

            authClient.Verify(x => x.AuthSyncAccountAsync("/repo", It.IsAny<CancellationToken>()), Times.Once);
            authClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);
            authClient.Verify(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()), Times.Once);
            authClient.Verify(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SyncMethodNotFound_DisablesSyncUntilRestart()
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
            var link = (JObject)_bundle.Fixture("project-link", "full", FixtureForm.Wire);
            var repo = new AuthRepo("/repo", "repo");

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
            authClient.Setup(x => x.AuthSyncAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DevtoolsException("Method not found", -32601, "trace"));
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);
            authClient.Setup(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(link);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            await service.SetRepoAsync(repo).ConfigureAwait(false);

            authClient.Verify(x => x.AuthSyncAccountAsync("/repo", It.IsAny<CancellationToken>()), Times.Once);

            var repo2 = new AuthRepo("/repo2", "repo2");
            authClient.Setup(x => x.ProjectLinkGetAsync("/repo2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(link);

            await service.SetRepoAsync(repo2).ConfigureAwait(false);

            authClient.Verify(x => x.AuthSyncAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

            authClient.Setup(x => x.AuthSyncAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = true });
            await scheduler.WhenIdleAsync().ConfigureAwait(false);

            authClient.Verify(x => x.AuthSyncAccountAsync("/repo2", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task RepoChange_ResetsLinkFieldsAndReReadsTheLink()
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
            var link1 = (JObject)_bundle.Fixture("project-link", "full", FixtureForm.Wire);
            var link2 = (JObject)_bundle.Fixture("project-link", "remote", FixtureForm.Wire);
            var repo1 = new AuthRepo("/repo1", "repo1");
            var repo2 = new AuthRepo("/repo2", "repo2");

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);
            authClient.Setup(x => x.ProjectLinkGetAsync("/repo1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(link1);
            authClient.Setup(x => x.ProjectLinkGetAsync("/repo2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(link2);
            authClient.Setup(x => x.AuthSyncAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            await service.SetRepoAsync(repo1).ConfigureAwait(false);

            Assert.AreEqual("/repo1", service.Current.Repo.Path);
            Assert.IsNotNull(service.Current.LinkedProject);

            await service.SetRepoAsync(repo2).ConfigureAwait(false);

            Assert.AreEqual("/repo2", service.Current.Repo.Path);
            Assert.IsNotNull(service.Current.LinkedProject);
            authClient.Verify(x => x.ProjectLinkGetAsync("/repo2", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task RepoChangeWhileNotLoaded_OnlyPublishesTheRepo()
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
            var repo = new AuthRepo("/repo", "repo");

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signingInStatus);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            await service.SetRepoAsync(repo).ConfigureAwait(false);

            Assert.AreEqual("/repo", service.Current.Repo.Path);
            authClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Never);
            authClient.Verify(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()), Times.Never);
            authClient.Verify(x => x.ProjectLinkGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task RepoChangeToAnAccountOfAnotherOwner_ReloadsAccountData()
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
            var link = (JObject)_bundle.Fixture("project-link", "full", FixtureForm.Wire);
            var repo = new AuthRepo("/repo", "repo");

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status1);
            authClient.Setup(x => x.AuthSyncAccountAsync("/repo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(status2);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);
            authClient.Setup(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(link);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            await service.SetRepoAsync(repo).ConfigureAwait(false);

            authClient.Verify(x => x.AuthSyncAccountAsync("/repo", It.IsAny<CancellationToken>()), Times.Once);
            authClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task StaleRepoLookup_IsDropped()
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
            var link1 = (JObject)_bundle.Fixture("project-link", "full", FixtureForm.Wire);
            link1["project"]["name"] = "Project 1";
            var link2 = (JObject)_bundle.Fixture("project-link", "full", FixtureForm.Wire);
            link2["project"]["name"] = "Project 2";
            var repo1 = new AuthRepo("/repo1", "repo1");
            var repo2 = new AuthRepo("/repo2", "repo2");

            var tcs1 = new TaskCompletionSource<JObject>();
            var tcs2 = new TaskCompletionSource<JObject>();
            var linkCalls = 0;

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
            authClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
            authClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);
            authClient.Setup(x => x.ProjectLinkGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string path, CancellationToken _) =>
                {
                    linkCalls++;
                    return linkCalls == 1 ? tcs1.Task : tcs2.Task;
                });
            authClient.Setup(x => x.AuthSyncAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            var task1 = service.SetRepoAsync(repo1);
            var task2 = service.SetRepoAsync(repo2);

            tcs2.SetResult(link2);
            tcs1.SetResult(link1);

            await Task.WhenAll(task1, task2).ConfigureAwait(false);

            var projectName = service.Current.LinkedProject?["name"]?.Value<string>();
            Assert.AreEqual("Project 2", projectName);
        }
    }
}
