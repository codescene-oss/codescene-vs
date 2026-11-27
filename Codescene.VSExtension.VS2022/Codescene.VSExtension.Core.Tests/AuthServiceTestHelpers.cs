// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    internal sealed class AuthServiceKit
    {
        private readonly ContractBundle _bundle;

        public AuthServiceKit(ContractBundle bundle, bool enabled = true)
        {
            _bundle = bundle;
            Settings.SetupGet(x => x.BrowserSignInEnabled).Returns(enabled);
            Host.Setup(x => x.OpenExternalAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            Host.Setup(x => x.RevealMonitorAsync()).Returns(Task.CompletedTask);
            Service = new AuthService(
                Client.Object,
                AuthClient.Object,
                Settings.Object,
                Host.Object,
                Telemetry.Object,
                new FakeDebounceService(),
                Scheduler,
                Logger);
            Service.SnapshotChanged += (sender, snapshot) => Snapshots.Add(snapshot);
        }

        public Mock<IIdeServerClient> Client { get; } = new Mock<IIdeServerClient>();

        public Mock<IIdeServerAuthClient> AuthClient { get; } = new Mock<IIdeServerAuthClient>();

        public Mock<IAuthSettings> Settings { get; } = new Mock<IAuthSettings>();

        public Mock<IAuthHost> Host { get; } = new Mock<IAuthHost>();

        public Mock<ITelemetryManager> Telemetry { get; } = new Mock<ITelemetryManager>();

        public InlineAsyncTaskScheduler Scheduler { get; } = new InlineAsyncTaskScheduler();

        public FakeLogger Logger { get; } = new FakeLogger();

        public List<AuthSnapshot> Snapshots { get; } = new List<AuthSnapshot>();

        public AuthService Service { get; }

        public JObject Status(string variant) => (JObject)_bundle.Fixture("auth-status", variant, FixtureForm.Wire);

        public JObject Link(string variant) => (JObject)_bundle.Fixture("project-link", variant, FixtureForm.Wire);

        public JObject Login() => (JObject)_bundle.Fixture("login-pending", "full", FixtureForm.Wire);

        public void Start(string configureVariant = "signed-out", bool capable = true)
        {
            AuthClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Status(configureVariant));
            Service.Start();
            var capabilities = capable ? (JArray)_bundle.Fixture("start-notification", "full", FixtureForm.Wire)["capabilities"] : new JArray();
            var metadata = new ServerMetadata { Capabilities = capabilities.ToObject<List<string>>() };
            Client.Raise(x => x.ServerStarted += null, Client.Object, new ServerStartEvent { Metadata = metadata, Restart = false });
        }

        public void SetupAccountData(string linkVariant = "no-project-candidates", string syncVariant = "signed-in")
        {
            AuthClient.Setup(x => x.AuthSyncAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Status(syncVariant));
            AuthClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire));
            AuthClient.Setup(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire));
            AuthClient.Setup(x => x.ProjectLinkGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Link(linkVariant));
        }

        public void SetupSeveralAccounts()
        {
            var accounts = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);
            var other = (JObject)accounts[0].DeepClone();
            other["id"] = "43";
            other["signed-in-locally"] = false;
            accounts.Add(other);
            AuthClient.Setup(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (JArray)accounts.DeepClone());
        }

        public void RaiseStatus(JObject status)
        {
            AuthClient.Raise(x => x.AuthStatusChanged += null, AuthClient.Object, status);
        }

        public void RaiseStatus(string variant) => RaiseStatus(Status(variant));

        public Task IdleAsync() => Scheduler.WhenIdleAsync();
    }

    internal sealed class InlineAsyncTaskScheduler : IAsyncTaskScheduler
    {
        private readonly List<Task> _tasks = new List<Task>();

        public void Schedule(Func<Task> asyncWork)
        {
            Track(asyncWork());
        }

        public void Schedule(Func<CancellationToken, Task> asyncWork)
        {
            Track(asyncWork(CancellationToken.None));
        }

        public void Schedule(Func<CancellationToken, Task> asyncWork, CancellationToken cancellationToken)
        {
            Track(asyncWork(cancellationToken));
        }

        public async Task WhenIdleAsync()
        {
            Task[] pending;
            lock (_tasks)
            {
                pending = _tasks.ToArray();
            }

            var all = Task.WhenAll(pending);
            if (await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(5))) != all)
            {
                throw new TimeoutException("Scheduled auth work did not finish.");
            }

            await all;
        }

        private void Track(Task task)
        {
            lock (_tasks)
            {
                _tasks.Add(task);
            }
        }
    }

    internal sealed class FakeDebounceService : IDebounceService
    {
        public List<(string Key, int DelayMs)> Calls { get; } = new List<(string, int)>();

        public void Debounce(string key, Action action, TimeSpan delay)
        {
            Calls.Add((key, (int)delay.TotalMilliseconds));
            action();
        }

        public void Throttle(string key, Action action, TimeSpan interval)
        {
            action();
        }

        public void Cancel(string key)
        {
        }
    }
}
