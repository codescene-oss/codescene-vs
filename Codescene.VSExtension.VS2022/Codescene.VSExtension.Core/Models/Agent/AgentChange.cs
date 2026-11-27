// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Agent
{
    public class AgentChange
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("change_type")]
        public string ChangeType { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("replacements")]
        public AgentReplacement[] Replacements { get; set; }

        [JsonProperty("whole_file_content")]
        public string WholeFileContent { get; set; }
    }
}
