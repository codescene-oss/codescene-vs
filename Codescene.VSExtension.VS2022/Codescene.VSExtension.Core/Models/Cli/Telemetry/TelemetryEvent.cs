using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    public override bool Equals(object obj)
    {
        if (!(obj is TelemetryEvent)) return false;
        var other = (TelemetryEvent)obj;

        return string.Equals(this.EventName, other.EventName) &&
               string.Equals(this.UserId, other.UserId) &&
               string.Equals(this.EditorType, other.EditorType) &&
               string.Equals(this.ExtensionVersion, other.ExtensionVersion) &&
               Nullable.Equals(this.Internal, other.Internal) &&
               DictionaryEquals(this._additionalProperties, other._additionalProperties);
    }

    private static bool DictionaryEquals(IDictionary<string, JToken> d1, IDictionary<string, JToken> d2)
    {
        if (d1 == d2) return true;
        if (d1 == null || d2 == null || d1.Count != d2.Count) return false;

        foreach (var kvp in d1)
        {
            if (!d2.TryGetValue(kvp.Key, out var val2))
                return false;

            if (!JToken.DeepEquals(kvp.Value, val2))
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (EventName != null ? EventName.GetHashCode() : 0);
            hash = hash * 31 + (UserId != null ? UserId.GetHashCode() : 0);
            hash = hash * 31 + (EditorType != null ? EditorType.GetHashCode() : 0);
            hash = hash * 31 + (ExtensionVersion != null ? ExtensionVersion.GetHashCode() : 0);
            hash = hash * 31 + (Internal.HasValue ? Internal.Value.GetHashCode() : 0);
            foreach (var kv in _additionalProperties)
            {
                hash = hash * 31 + kv.Key.GetHashCode();
                hash = hash * 31 + (kv.Value != null ? kv.Value.GetHashCode() : 0);
            }
            return hash;
        }
    }
}
