using Newtonsoft.Json;

namespace CodeLensShared
{
    public class CsReview
    {
        public float Score { get; set; }
        public Review[] Review { get; set; }

        [JsonProperty("raw-score")]
        public RawScore RawScore { get; set; }
    }
}