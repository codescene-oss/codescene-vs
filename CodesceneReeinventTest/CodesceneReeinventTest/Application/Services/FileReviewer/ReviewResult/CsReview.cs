using Newtonsoft.Json;

namespace CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;

public class CsReview
{
    public float Score { get; set; }
    public Review[] Review { get; set; }

    [JsonProperty("raw-score")]
    public RawScore RawScore { get; set; }
}