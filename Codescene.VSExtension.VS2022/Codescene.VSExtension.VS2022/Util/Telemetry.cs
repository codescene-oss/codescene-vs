using Newtonsoft.Json;

namespace Codescene.VSExtension.VS2022.Util;

public static class Telemetry
{
    public class EventProperties
    {
        public string EventName { get; set; }
        public string DeviceId { get; set; }
        public bool Internal { get; set; } = false;
    }

    public static string BuildTelemetryEvent(EventProperties eventProperties)
    {
        var telemetryEvent = new TelemetryEvent
        {
            EventName = eventProperties.EventName,
            ExtensionVersion = ExtensionMetadataProvider.GetVersion(),
            Internal = eventProperties.Internal
        };

        // Serialize to JSON string
        return JsonConvert.SerializeObject(telemetryEvent);
    }
}

