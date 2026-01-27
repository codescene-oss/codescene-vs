namespace Codescene.VSExtension.Core.Models
{
    public class CodeSmellTooltipModel
    {
        public string Category { get; set; }
        public string Details { get; set; }
        public string Path { get; set; }
        public string FunctionName { get; set; }
        public CodeRangeModel Range { get; set; }
        public CodeRangeModel FunctionRange { get; set; } = null;
    }
}
