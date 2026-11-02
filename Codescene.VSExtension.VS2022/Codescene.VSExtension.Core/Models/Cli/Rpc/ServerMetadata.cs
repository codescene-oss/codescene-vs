// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ServerMetadata
    {
        [JsonProperty("sha")]
        public string Sha { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("capabilities")]
        public IReadOnlyList<string> Capabilities { get; set; } = new List<string>();
    }
}
