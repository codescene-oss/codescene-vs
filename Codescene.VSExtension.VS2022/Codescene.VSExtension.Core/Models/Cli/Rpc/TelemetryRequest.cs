// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class TelemetryRequest
    {
        [JsonProperty("event")]
        public TelemetryEvent Event { get; set; }
    }
}
