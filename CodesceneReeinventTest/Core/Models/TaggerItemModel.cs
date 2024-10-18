namespace CodesceneReeinventTest.Core.Models
{
    public class TaggerItemModel
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string TooltipText { get; set; }
    }
}
