using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliCodeSmellModel
    {
        /// <summary>
        /// Name of codesmell.
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; }

        /// <summary>
        /// Details about codesmell, for example nesting depth.
        /// </summary>
        [JsonProperty("details")]
        public string Details { get; set; }

        /// <summary>
        /// Range for highlighting this code smell.
        /// </summary>
        [JsonProperty("highlight-range")]
        public CliRangeModel Range { get; set; }
    }
}
