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
    public class AuthServiceConfigurationTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void OptionOnWithCapability_ConfiguresWithBothKeysThenPublishesActive()
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

            AuthSnapshot captured = null;
            service.SnapshotChanged += (_, s) => captured = s;

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Verify(x => x.AuthConfigureAsync("https://example.com", "token", It.IsAny<CancellationToken>()), Times.Once);
            Assert.IsTrue(service.Current.Active);
            Assert.IsNotNull(service.Current.Status);
            Assert.IsNotNull(captured);
            Assert.IsTrue(captured.Active);
        }

        [TestMethod]
        public void OptionOnWithCapability_WithBlankSettings_SendsNulls()
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
            settings.Setup(x => x.ServerUrl).Returns("  ");
            settings.Setup(x => x.AccessToken).Returns("  ");

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Verify(x => x.AuthConfigureAsync(null, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void OptionOnWithCapability_PassesTheDecryptedToken()
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
            settings.Setup(x => x.AccessToken).Returns("decrypted-token");

            var metadata = new ServerMetadata
            {
                Capabilities = new List<string> { "auth-v1" },
            };

            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Verify(x => x.AuthConfigureAsync("https://example.com", "decrypted-token", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Restart_ConfiguresAgain()
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

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = true });

            authClient.Verify(x => x.AuthConfigureAsync("https://example.com", "token", It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public void SettingsChanged_IsDebouncedIntoOneConfigure()
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

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            debounce.Calls.Clear();
            settings.Raise(x => x.Changed += null, settings.Object, EventArgs.Empty);

            Assert.AreEqual(1, debounce.Calls.Count);
            Assert.AreEqual("auth-settings", debounce.Calls[0].Key);
            Assert.AreEqual(500, debounce.Calls[0].DelayMs);
        }

        [TestMethod]
        public void StaleConfigureResult_IsDropped()
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
            var status2 = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);

            var tcs1 = new TaskCompletionSource<JObject>();
            var tcs2 = new TaskCompletionSource<JObject>();
            var calls = 0;

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    calls++;
                    return calls == 1 ? tcs1.Task : tcs2.Task;
                });

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
            settings.Raise(x => x.Changed += null, settings.Object, EventArgs.Empty);

            tcs2.SetResult(status2);
            tcs1.SetResult(status1);

            Task[] tasksToWait;
            lock (runningTasks)
            {
                tasksToWait = runningTasks.ToArray();
            }

            Task.WaitAll(tasksToWait, TimeSpan.FromSeconds(5));

            var currentState = AuthStatusFields.State(service.Current.Status);
            Assert.AreEqual("signed-out", currentState);
        }

        [TestMethod]
        public void ConfigureFailure_KeepsTheLastStatus()
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

            var initialStatus = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            var configureCallCount = 0;
            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    configureCallCount++;
                    if (configureCallCount == 1)
                    {
                        return Task.FromResult(initialStatus);
                    }

                    throw new InvalidOperationException("configure failed");
                });

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();
            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            var currentBeforeFailure = service.Current;

            settings.Raise(x => x.Changed += null, settings.Object, EventArgs.Empty);

            var currentAfterFailure = service.Current;

            Assert.IsTrue(currentBeforeFailure.Active);
            Assert.AreSame(currentBeforeFailure.Status, currentAfterFailure.Status);
            Assert.IsTrue(logger.Warnings.Exists(m => m.Contains("[auth] auth-configure failed; keeping the last status")));
        }

        [TestMethod]
        public void MetadataAlreadyReceivedBeforeStart_IsHandled()
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

            client.Setup(x => x.Metadata).Returns(metadata);

            authClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);

            var service = new AuthService(client.Object, authClient.Object, settings.Object, host.Object, telemetry.Object, debounce, scheduler, logger);

            service.Start();

            authClient.Verify(x => x.AuthConfigureAsync("https://example.com", "token", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Start_Twice_DoesNotSubscribeTwice()
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

            service.Start();
            service.Start();

            client.Raise(x => x.ServerStarted += null, client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });

            authClient.Verify(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
