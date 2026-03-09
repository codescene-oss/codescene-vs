// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Threading;
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

        public async Task SendErrorTelemetryAsync(Exception ex, string context, Dictionary<string, object> extraData = null, CancellationToken cancellationToken = default)
        {
            await Mock.Object.SendErrorTelemetryAsync(ex, context, extraData, cancellationToken);
        }
    }
}
