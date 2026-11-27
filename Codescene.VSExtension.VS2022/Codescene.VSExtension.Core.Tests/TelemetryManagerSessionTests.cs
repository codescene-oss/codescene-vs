// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Application.Telemetry;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TelemetryManagerSessionTests
    {
        private const string RepositoryRoot = @"C:\Users\alice\src\repo";

        private Mock<IIdeServerClient> _client;
        private Mock<ITelemetryRepositoryPaths> _repositoryPaths;
        private Mock<IAuthService> _authService;
        private List<JObject> _sent;
        private TelemetryManager _manager;

        [TestInitialize]
        public void Setup()
        {
            TelemetryUtils.TelemetryEnabledOverrideForTests = true;
            _sent = new List<JObject>();
            _client = new Mock<IIdeServerClient>();
            _client.Setup(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Callback<object, CancellationToken>((payload, _) =>
                {
                    lock (_sent)
                    {
                        _sent.Add(JObject.FromObject(payload));
                    }
                })
                .Returns(Task.CompletedTask);
            _repositoryPaths = new Mock<ITelemetryRepositoryPaths>();
            _repositoryPaths.Setup(x => x.GetWorkspaceRepositoryPathsAsync())
                .ReturnsAsync(new[] { @"C:\Users\alice\src\workspace" });
            _authService = new Mock<IAuthService>();
            _authService.SetupGet(x => x.Current).Returns(AuthSnapshot.Inactive);
            _manager = CreateManager(TimeSpan.FromMinutes(5));
        }

        [TestCleanup]
        public void TearDown()
        {
            TelemetryUtils.TelemetryEnabledOverrideForTests = null;
        }

        [TestMethod]
        public async Task SendTelemetryAsync_AddsStableSessionIdAndEventTime()
        {
            await _manager.SendTelemetryAsync("first");
            await _manager.SendTelemetryAsync("second");

            Assert.HasCount(2, _sent);
            Assert.AreEqual(_manager.SessionId, (string)_sent[0]["session-id"]);
            Assert.AreEqual(_manager.SessionId, (string)_sent[1]["session-id"]);
            Assert.IsTrue(Guid.TryParse(_manager.SessionId, out _));
            Assert.IsNotNull(_sent[0]["event-time"]);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_WhenSignedIn_SendsTheCodeSceneUserIdAsUserId()
        {
            var status = new JObject { ["state"] = "signed-in", ["user"] = new JObject { ["id"] = "1234", ["name"] = "Fake User" } };
            _authService.SetupGet(x => x.Current).Returns(AuthSnapshot.Inactive.WithActive(true).WithStatus(status));

            await _manager.SendTelemetryAsync("docs/open-interactive-panel");

            Assert.AreEqual("1234", (string)_sent.Single()["user-id"]);
        }

        [TestMethod]
        [DataRow("signed-out")]
        [DataRow("signing-in")]
        public async Task SendTelemetryAsync_WhenNotSignedIn_OmitsUserId(string state)
        {
            var status = new JObject { ["state"] = state, ["user"] = new JObject { ["id"] = "1234" } };
            _authService.SetupGet(x => x.Current).Returns(AuthSnapshot.Inactive.WithActive(true).WithStatus(status));

            await _manager.SendTelemetryAsync("docs/open-interactive-panel");

            Assert.IsNull(_sent.Single()["user-id"]);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_WhenAuthIsInactive_OmitsUserId()
        {
            await _manager.SendTelemetryAsync("docs/open-interactive-panel");

            Assert.IsNull(_sent.Single()["user-id"]);
        }

        [TestMethod]
        public async Task SendTelemetryAsync_WithExplicitRepositoryPaths_SendsThemUnredactedOnlyAsRepositoryPaths()
        {
            var data = new Dictionary<string, object>
            {
                { "trace-id", "t-1" },
                { "repository-paths", new[] { RepositoryRoot } },
            };

            await _manager.SendTelemetryAsync("ai/refactor/applied", data);

            var evt = _sent.Single();
            CollectionAssert.AreEqual(new[] { RepositoryRoot }, evt["repository-paths"].ToObject<string[]>());
            Assert.AreEqual("t-1", (string)evt["trace-id"]);
            Assert.IsFalse(evt.Properties().Where(p => p.Name != "repository-paths").Any(p => p.Value.ToString().Contains("alice")));
            Assert.IsTrue(data.ContainsKey("repository-paths"));
        }

        [TestMethod]
        public async Task SendTelemetryAsync_WithoutRepositoryPaths_UsesWorkspaceRepositories()
        {
            await _manager.SendTelemetryAsync("docs/open-interactive-panel");

            CollectionAssert.AreEqual(
                new[] { @"C:\Users\alice\src\workspace" },
                _sent.Single()["repository-paths"].ToObject<string[]>());
        }

        [TestMethod]
        public async Task SendTelemetryAsync_WithNoWorkspaceRepositories_OmitsRepositoryPaths()
        {
            _repositoryPaths.Setup(x => x.GetWorkspaceRepositoryPathsAsync()).ReturnsAsync(Array.Empty<string>());

            await _manager.SendTelemetryAsync("docs/open-interactive-panel");

            Assert.IsNull(_sent.Single()["repository-paths"]);
        }

        [TestMethod]
        public async Task HeldEvents_AreSentOnReady_WithSessionStartedFirstAndOriginalEventTime()
        {
            var generation = _manager.HoldRouting();

            await _manager.SendTelemetryAsync("auth/sign-in-started");
            await _manager.SendTelemetryAsync("session/started");
            Assert.IsEmpty(_sent);

            var beforeReady = DateTime.UtcNow;
            _manager.RoutingReady(generation);
            await WaitForSentAsync(2);

            CollectionAssert.AreEqual(
                new[] { "vs/session/started", "vs/auth/sign-in-started" },
                _sent.Select(e => (string)e["event-name"]).ToArray());
            Assert.IsTrue(_sent[0]["event-time"].ToObject<DateTime>().ToUniversalTime() <= beforeReady);
        }

        [TestMethod]
        public async Task HeldEvents_AreReleasedWhenBoundElapses()
        {
            var manager = CreateManager(TimeSpan.FromMilliseconds(20));
            manager.HoldRouting();

            await manager.SendTelemetryAsync("session/started");
            await WaitForSentAsync(1);

            Assert.AreEqual("vs/session/started", (string)_sent.Single()["event-name"]);
        }

        [TestMethod]
        public async Task EndSessionAsync_SendsSessionEndedAndReleasesHeldEvents()
        {
            _manager.HoldRouting();
            await _manager.SendTelemetryAsync("session/started");

            await _manager.EndSessionAsync(TimeSpan.FromSeconds(3));

            CollectionAssert.AreEquivalent(
                new[] { "vs/session/started", "vs/session/ended" },
                _sent.Select(e => (string)e["event-name"]).ToArray());
        }

        [TestMethod]
        public async Task EndSessionAsync_WaitsAtMostTheTimeoutForInFlightEvents()
        {
            _client.Setup(x => x.TelemetryAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(TimeSpan.FromSeconds(30)));

            var started = DateTime.UtcNow;
            await _manager.EndSessionAsync(TimeSpan.FromMilliseconds(100));

            Assert.IsLessThan(TimeSpan.FromSeconds(10), DateTime.UtcNow - started);
        }

        [TestMethod]
        public async Task EndSessionAsync_WhenTelemetryDisabled_SendsNothing()
        {
            TelemetryUtils.TelemetryEnabledOverrideForTests = false;

            await _manager.EndSessionAsync(TimeSpan.FromSeconds(3));

            Assert.IsEmpty(_sent);
        }

        private TelemetryManager CreateManager(TimeSpan routingBound)
        {
            var metadata = new Mock<IExtensionMetadataProvider>();
            metadata.Setup(x => x.GetVersion()).Returns("1.0.0");
            metadata.Setup(x => x.GetEditorVersion()).Returns("18.1.1");

            return new TelemetryManager(
                new Mock<ILogger>().Object,
                _client.Object,
                metadata.Object,
                null,
                _repositoryPaths.Object,
                new Lazy<IAuthService>(() => _authService.Object),
                routingBound);
        }

        private async Task WaitForSentAsync(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                lock (_sent)
                {
                    if (_sent.Count >= count)
                    {
                        return;
                    }
                }

                await Task.Delay(10);
            }
        }
    }
}
