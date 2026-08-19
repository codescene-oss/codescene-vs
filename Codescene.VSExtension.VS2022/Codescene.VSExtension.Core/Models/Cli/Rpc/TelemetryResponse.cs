// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class TelemetryResponse
    {
        [JsonProperty("status")]
        public int Status { get; set; }
    }
}
