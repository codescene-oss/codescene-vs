using Newtonsoft.Json;

namespace Core.Models.ReviewResultModel
{
    public class HighlightRangeModel
    {
        [JsonProperty("start-line")]
        public int Startline { get; set; }
        [JsonProperty("start-column")]
        public int StartColumn { get; set; }
        [JsonProperty("end-line")]
        public int EndLine { get; set; }
        [JsonProperty("end-column")]
        public int EndColumn { get; set; }
    }
}
