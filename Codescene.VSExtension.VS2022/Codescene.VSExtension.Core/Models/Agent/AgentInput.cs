// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Models.Agent
{
    public class AgentInput
    {
        [JsonProperty("@context")]
        public JObject Context { get; set; }

        [JsonProperty("task_id")]
        public string TaskId { get; set; }

        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("line")]
        public int Line { get; set; }

        [JsonProperty("smell")]
        public string Smell { get; set; }
    }
}
