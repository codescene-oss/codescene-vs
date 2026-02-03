// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class ConfidenceModel
    {
        /// <summary>
        /// Gets or sets confidence level.
        /// </summary>
        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("recommended-action")]
        public RecommendedActionModel RecommendedAction { get; set; }

        /// <summary>
        /// Gets or sets header for use when presenting the reason summaries.
        /// </summary>
        [JsonProperty("review-header")]
        public string ReviewHeader { get; set; }

        /// <summary>
        /// Gets or sets title for presentation.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }
    }
}
