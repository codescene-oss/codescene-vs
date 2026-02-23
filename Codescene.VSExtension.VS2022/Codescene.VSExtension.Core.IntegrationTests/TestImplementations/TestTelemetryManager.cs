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

        public async Task SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null)
        {
            await Mock.Object.SendTelemetryAsync(eventName, additionalEventData);
        }

        public async Task SendErrorTelemetryAsync(Exception ex, string context, Dictionary<string, object> extraData = null)
        {
            await Mock.Object.SendErrorTelemetryAsync(ex, context, extraData);
        }
    }
}
