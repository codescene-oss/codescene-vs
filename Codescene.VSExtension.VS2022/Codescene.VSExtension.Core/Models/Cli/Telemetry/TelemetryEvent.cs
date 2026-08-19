// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Models.Cli.Telemetry
{
    public class TelemetryEvent
    {
        [JsonProperty("event-name", NullValueHandling = NullValueHandling.Ignore)]
        public string EventName { get; set; }

        [JsonProperty("user-id", NullValueHandling = NullValueHandling.Ignore)]
        public string UserId { get; set; }

        [JsonProperty("editor-type", NullValueHandling = NullValueHandling.Ignore)]
        public string EditorType { get; set; }

        [JsonProperty("editor-version", NullValueHandling = NullValueHandling.Ignore)]
        public string EditorVersion { get; set; }

        [JsonProperty("extension-version", NullValueHandling = NullValueHandling.Ignore)]
        public string ExtensionVersion { get; set; }

        [JsonProperty("internal", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Internal { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

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

        public TelemetryEvent WithEditorVersion(string editorVersion)
        {
            this.EditorVersion = editorVersion;
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
    }
}
