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

        public void SendTelemetry(string eventName, Dictionary<string, object> additionalEventData = null)
        {
            Mock.Object.SendTelemetry(eventName, additionalEventData);
        }

        public void SendErrorTelemetry(Exception ex, string context, Dictionary<string, object> extraData = null)
        {
            Mock.Object.SendErrorTelemetry(ex, context, extraData);
        }
    }
}
