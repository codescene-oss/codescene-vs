using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public class TelemetryEvent
{
    [JsonProperty("event-name", NullValueHandling = NullValueHandling.Ignore)]
    public string EventName { get; set; }

    [JsonProperty("user-id", NullValueHandling = NullValueHandling.Ignore)]
    public string UserId { get; set; }

    [JsonProperty("editor-type", NullValueHandling = NullValueHandling.Ignore)]
    public string EditorType { get; set; }

    [JsonProperty("extension-version", NullValueHandling = NullValueHandling.Ignore)]
    public string ExtensionVersion { get; set; }

    [JsonProperty("internal", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Internal { get; set; }

    [JsonExtensionData]
    private IDictionary<string, JToken> _additionalProperties = new Dictionary<string, JToken>();

    public IDictionary<string, JToken> AdditionalProperties
    {
        get { return _additionalProperties; }
        set { _additionalProperties = value; }
    }

    public TelemetryEvent() { }

    public TelemetryEvent(
        string eventName,
        string userId,
        string editorType,
        string extensionVersion,
        bool? internalFlag)
    {
        this.EventName = eventName;
        this.UserId = userId;
        this.EditorType = editorType;
        this.ExtensionVersion = extensionVersion;
        this.Internal = internalFlag;
    }

    public TelemetryEvent WithEventName(string eventName)
    {
        this.EventName = eventName;
        return this;
    }

    public TelemetryEvent WithUserId(string userId)
    {
        this.UserId = userId;
        return this;
    }

    public TelemetryEvent WithEditorType(string editorType)
    {
        this.EditorType = editorType;
        return this;
    }

    public TelemetryEvent WithExtensionVersion(string extensionVersion)
    {
        this.ExtensionVersion = extensionVersion;
        return this;
    }

    public TelemetryEvent WithInternal(bool? internalFlag)
    {
        this.Internal = internalFlag;
        return this;
    }

    public TelemetryEvent WithAdditionalProperty(string name, object value)
    {
        _additionalProperties[name] = JToken.FromObject(value);
        return this;
    }
}
