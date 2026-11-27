// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthServiceTelemetryRoutingTests
    {
        private static ContractBundle _bundle = null!;

        private readonly List<string> _calls = new List<string>();
        private int _generation;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public async Task Start_HoldsRoutingUntilAuthConfigureSettles()
        {
            var kit = CreateKit();
            var configure = new TaskCompletionSource<JObject>();
            kit.AuthClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    _calls.Add("configure");
                    return configure.Task;
                });

            kit.Service.Start();
            RaiseStart(kit, restart: false);

            CollectionAssert.AreEqual(new[] { "hold:1", "hold:2", "configure" }, _calls);

            configure.SetResult(kit.Status("signed-out"));
            await kit.IdleAsync();

            CollectionAssert.AreEqual(new[] { "hold:1", "hold:2", "configure", "ready:2" }, _calls);
        }

        [TestMethod]
        public async Task Start_WhenConfigureFails_StillReleasesRouting()
        {
            var kit = CreateKit();
            kit.AuthClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            kit.Service.Start();
            RaiseStart(kit, restart: false);
            await kit.IdleAsync();

            Assert.AreEqual("ready:2", _calls[_calls.Count - 1]);
        }

        [TestMethod]
        public async Task Start_WithSignInDisabled_ReleasesRoutingWithoutConfigure()
        {
            var kit = CreateKit(enabled: false);

            kit.Service.Start();
            RaiseStart(kit, restart: false);
            await kit.IdleAsync();

            CollectionAssert.AreEqual(new[] { "hold:1", "hold:2", "ready:2" }, _calls);
            kit.AuthClient.Verify(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task ServerStopThenRestart_HoldsAgainUntilRestartIsHandled()
        {
            var kit = CreateKit();
            kit.AuthClient.Setup(x => x.AuthConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => kit.Status("signed-out"));
            kit.Service.Start();
            RaiseStart(kit, restart: false);
            await kit.IdleAsync();
            _calls.Clear();

            kit.Client.Raise(x => x.ServerStopped += null, kit.Client.Object, EventArgs.Empty);
            RaiseStart(kit, restart: true);
            await kit.IdleAsync();

            CollectionAssert.AreEqual(new[] { "hold:3", "hold:4", "ready:4" }, _calls);
        }

        private static void RaiseStart(AuthServiceKit kit, bool restart)
        {
            var capabilities = (JArray)_bundle.Fixture("start-notification", "full", FixtureForm.Wire)["capabilities"];
            var metadata = new ServerMetadata { Capabilities = capabilities.ToObject<List<string>>() };
            kit.Client.Raise(x => x.ServerStarted += null, kit.Client.Object, new ServerStartEvent { Metadata = metadata, Restart = restart });
        }

        private AuthServiceKit CreateKit(bool enabled = true)
        {
            var kit = new AuthServiceKit(_bundle, enabled);
            kit.Telemetry.Setup(x => x.HoldRouting()).Returns(() =>
            {
                _generation++;
                _calls.Add("hold:" + _generation);
                return _generation;
            });
            kit.Telemetry.Setup(x => x.RoutingReady(It.IsAny<int>())).Callback<int>(g => _calls.Add("ready:" + g));
            return kit;
        }
    }
}
