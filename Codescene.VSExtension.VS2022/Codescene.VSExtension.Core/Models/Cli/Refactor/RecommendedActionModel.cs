// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class RecommendedActionModel
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }
    }
}
