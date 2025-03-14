namespace Codescene.VSExtension.Core.Models.ReviewResult
{
    public class Bumps
    {
        public int Fnswithsmallbumps { get; set; }
        public int Fnswithlargebumps { get; set; }
        public int Fnswithseverebumps { get; set; }
        public WorstBump Worstbump { get; set; }
        public BumpsBySeverity[] Bumpsbyseverity { get; set; }
    }
}
