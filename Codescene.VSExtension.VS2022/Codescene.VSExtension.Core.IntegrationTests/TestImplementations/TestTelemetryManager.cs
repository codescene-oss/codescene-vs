// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestTelemetryManager : ITelemetryManager
    {
        internal Mock<ITelemetryManager> Mock = new Mock<ITelemetryManager>();

        public async Task SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null, CancellationToken cancellationToken = default)
        {
            await Mock.Object.SendTelemetryAsync(eventName, additionalEventData, cancellationToken);
        }

        public int HoldRouting() => Mock.Object.HoldRouting();

        public void RoutingReady(int generation) => Mock.Object.RoutingReady(generation);

        public Task EndSessionAsync(TimeSpan timeout) => Mock.Object.EndSessionAsync(timeout) ?? Task.CompletedTask;
    }
}
