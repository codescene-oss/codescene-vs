using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class ConfidenceModel
    {
        /// <summary>
        /// Confidence level
        /// </summary>
        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("recommended-action")]
        public RecommendedActionModel RecommendedAction { get; set; }

        /// <summary>
        /// Header for use when presenting the reason summaries
        /// </summary>
        [JsonProperty("review-header")]
        public string ReviewHeader { get; set; }

        /// <summary>
        /// Title for presentation
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }
    }
}
