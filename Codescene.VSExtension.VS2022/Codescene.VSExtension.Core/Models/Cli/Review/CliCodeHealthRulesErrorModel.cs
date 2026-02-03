// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliCodeHealthRulesErrorModel
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets how to resolve the error.
        /// </summary>
        [JsonProperty("remedy")]
        public string Remedy { get; set; }
    }
}
