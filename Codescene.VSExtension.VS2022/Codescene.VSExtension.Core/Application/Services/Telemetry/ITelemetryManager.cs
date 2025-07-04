using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Telemetry
{
    public interface ITelemetryManager
    {
        void SendTelemetry(string eventName, Dictionary<string, object> additionalEventData = null);
    }
}
