using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliCodeSmellModel
    {
        /// <summary>
        /// Gets or sets name of codesmell.
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets details about codesmell, for example nesting depth.
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
