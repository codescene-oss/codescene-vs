using Newtonsoft.Json;

namespace Core.Models.ReviewResultModel
{
    public class CodeSmellModel
    {
        [JsonProperty("category")]
        public string Category { get; set; }
        [JsonProperty("range")]
        public RangeModel Range { get; set; }
        [JsonProperty("details")]
        public string Details { get; set; }
    }
}


