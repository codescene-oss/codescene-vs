// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class DeviceIdResponse
    {
        [JsonProperty("device-id")]
        public string DeviceId { get; set; }
    }
}
