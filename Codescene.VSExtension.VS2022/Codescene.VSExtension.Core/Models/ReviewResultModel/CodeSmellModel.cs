using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.ReviewResultModel
{
    public class CodeSmellModel
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }

        [JsonProperty("highlight-range")]
        public RangeModel Range { get; set; }

    }
}


