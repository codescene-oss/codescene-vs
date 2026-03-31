// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class ReasonModel
    {
        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("details")]
        public ReasonDetailsModel[] Details { get; set; }
    }
}
