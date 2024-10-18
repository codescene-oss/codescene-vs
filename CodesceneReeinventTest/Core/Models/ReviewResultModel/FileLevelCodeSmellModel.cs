using Newtonsoft.Json;

namespace Core.Models.ReviewResultModel
{
    public class FileLevelCodeSmellModel
    {
        [JsonProperty("category")]
        public string Category { get; set; }
        [JsonProperty("highlight-range")]
        public RangeModel Range { get; set; }
        [JsonProperty("details")]
        public string Details { get; set; }
    }
}


