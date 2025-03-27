using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class CliCodeSmellModel
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }

        [JsonProperty("highlight-range")]
        public CliRangeModel Range { get; set; }
    }
}


