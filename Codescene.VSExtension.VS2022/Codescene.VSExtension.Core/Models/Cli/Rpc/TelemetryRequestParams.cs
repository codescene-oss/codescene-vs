// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    internal class TelemetryRequestParams
    {
        [JsonProperty("event")]
        public object Event { get; set; }
    }
}
