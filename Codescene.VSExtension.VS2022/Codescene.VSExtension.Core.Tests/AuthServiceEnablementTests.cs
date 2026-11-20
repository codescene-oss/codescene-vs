// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
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
    public class AuthServiceEnablementTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void OptionOff_SendsNoAuthRequestAndPublishesNothing()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var scheduler = new InlineAsyncTaskScheduler();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(false);
            settings.Setup(x => x.ServerUrl).Returns(string.Empty);
            settings.Setup(x => x.AccessToken).Returns(string.Empty);

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            var snapshotCount = 0;
            service.SnapshotChanged += (_, __) => snapshotCount++;

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = true });
            settings.Raise(x => x.Changed += null, settings.Object, EventArgs.Empty);
            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, status);

            authClient.Verify(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            authClient.Verify(x => x.AuthStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
            authClient.Verify(x => x.AuthLoginAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.AreEqual(AuthSnapshot.Inactive, service.Current);
            Assert.AreEqual(0, snapshotCount);
        }

        [TestMethod]
        public void MissingCapability_BehavesAsOptionOff()
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
                Capabilities = new List<string>(),
            };

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            var snapshotCount = 0;
            service.SnapshotChanged += (_, __) => snapshotCount++;

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Verify(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.AreEqual(AuthSnapshot.Inactive, service.Current);
            Assert.AreEqual(0, snapshotCount);
        }

        [TestMethod]
        public void OptOutAfterOptIn_PublishesInactiveThenRestartsOnce()
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

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            client.Setup(x => x.RestartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            settings.Setup(x => x.BrowserSignInEnabled).Returns(false);
            settings.Raise(x => x.Changed += null, settings.Object, EventArgs.Empty);

            Assert.IsFalse(service.Current.Active);
            client.Verify(x => x.RestartAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.IsTrue(logger.Infos.Exists(m => m.Contains("[auth] browser sign-in turned off; restarting cs-ide")));
        }

        [TestMethod]
        public void OptOutWithoutOptIn_DoesNotRestart()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var scheduler = new InlineAsyncTaskScheduler();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(false);
            settings.Setup(x => x.ServerUrl).Returns(string.Empty);
            settings.Setup(x => x.AccessToken).Returns(string.Empty);

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            client.Verify(x => x.RestartAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public void OptOutRestartFailure_IsLoggedNotThrown()
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

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            client.Setup(x => x.RestartAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("restart failed"));

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            settings.Setup(x => x.BrowserSignInEnabled).Returns(false);
            settings.Raise(x => x.Changed += null, settings.Object, EventArgs.Empty);

            Assert.IsTrue(logger.Warnings.Exists(m => m.Contains("[auth] restart failed")));
        }

        [TestMethod]
        public void AuthStatusChanged_WhileActive_IsApplied()
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

            var initialStatus = (JObject)_bundle.Fixture("auth-status", "signing-in", FixtureForm.Wire);
            var newStatus = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(initialStatus);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, newStatus);

            Assert.AreEqual("signed-in", AuthStatusFields.State(service.Current.Status));
        }

        [TestMethod]
        public void AuthStatusChanged_WhileInactive_IsIgnored()
        {
            var client = new Mock<IIdeServerClient>();
            var authClient = new Mock<IIdeServerAuthClient>();
            var settings = new Mock<IAuthSettings>();
            var host = new Mock<IAuthHost>();
            var telemetry = new Mock<ITelemetryManager>();
            var debounce = new FakeDebounceService();
            var scheduler = new InlineAsyncTaskScheduler();
            var logger = new FakeLogger();

            settings.Setup(x => x.BrowserSignInEnabled).Returns(false);
            settings.Setup(x => x.ServerUrl).Returns(string.Empty);
            settings.Setup(x => x.AccessToken).Returns(string.Empty);

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, status);

            Assert.IsFalse(service.Current.Active);
            Assert.IsNull(service.Current.Status);
        }

        [TestMethod]
        public void Publish_DeduplicatesEqualSnapshots()
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

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            var snapshotCount = 0;
            service.SnapshotChanged += (_, __) => snapshotCount++;

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            var countAfterFirstPublish = snapshotCount;

            authClient.Raise(x => x.AuthStatusChanged += null, authClient.Object, status);

            Assert.AreEqual(countAfterFirstPublish, snapshotCount);
        }
    }
}
