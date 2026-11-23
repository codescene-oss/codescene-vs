// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Agent
{
    public class AgentReplacement
    {
        [JsonProperty("search")]
        public string Search { get; set; }

        [JsonProperty("replace")]
        public string Replace { get; set; }
    }
}
