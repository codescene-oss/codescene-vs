// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliCodeSmellModel
    {
        /// <summary>
        /// Gets or sets name of code smell.
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets details about code smell, for example nesting depth.
        /// </summary>
        [JsonProperty("details")]
        public string Details { get; set; }

        /// <summary>
        /// Gets or sets range for highlighting this code smell.
        /// </summary>
        [JsonProperty("highlight-range")]
        public CliRangeModel Range { get; set; }
    }
}
