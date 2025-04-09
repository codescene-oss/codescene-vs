namespace Codescene.VSExtension.Core.Models
{
    public class CodeSmellModel
    {
        public string Path { get; set; }
        public string Category { get; set; }
        public string Details { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
    }
}
