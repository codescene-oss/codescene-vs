namespace Core.Models.ReviewResult
{
    public class RawScore
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public float HighResolutionScore { get; set; }
        public float UnbiasedScore { get; set; }
        public Details Details { get; set; }
    }
}
