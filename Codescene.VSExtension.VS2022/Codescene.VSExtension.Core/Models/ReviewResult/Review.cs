namespace Codescene.VSExtension.Core.Models.ReviewResult
{
    public class Review
    {
        public string Category { get; set; }
        public Function[] Functions { get; set; }
        public string Description { get; set; }
        public int Indication { get; set; }
    }
}
