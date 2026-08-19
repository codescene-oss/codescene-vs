// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ServerStartMetadata
    {
        [JsonProperty("sha")]
        public string Sha { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }
}
