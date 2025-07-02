using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Telemetry
{
    public interface ITelemetryManager
    {
        bool IsTelemetryEnabled();
        void SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null);
    }
}
