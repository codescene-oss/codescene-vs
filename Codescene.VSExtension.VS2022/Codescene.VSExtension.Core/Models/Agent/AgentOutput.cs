// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Agent
{
    public class AgentOutput
    {
        [JsonProperty("schema_version")]
        public string SchemaVersion { get; set; }

        [JsonProperty("task_id")]
        public string TaskId { get; set; }

        [JsonProperty("fix_result")]
        public string FixResult { get; set; }

        [JsonProperty("confidence")]
        public string Confidence { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("reasoning")]
        public string Reasoning { get; set; }

        [JsonProperty("changes")]
        public AgentChange[] Changes { get; set; }

        [JsonProperty("generated_at")]
        public string GeneratedAt { get; set; }
    }
}
