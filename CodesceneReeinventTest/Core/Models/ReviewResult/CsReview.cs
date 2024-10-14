using Newtonsoft.Json;

namespace Core.Models.ReviewResult
{
    public class CsReview
    {
        public float Score { get; set; }
        public Review[] Review { get; set; }

        [JsonProperty("raw-score")]
        public RawScore RawScore { get; set; }
    }
}