using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestTelemetryManager : ITelemetryManager
    {
        internal Mock<ITelemetryManager> Mock = new();

        public void SendTelemetry(string eventName, System.Collections.Generic.Dictionary<string, object> additionalEventData = null)
        {
            Mock.Object.SendTelemetry(eventName, additionalEventData);
        }
    }
}

