namespace Codescene.VSExtension.Core.Models
{
    public class CodeSmellTooltipModel
    {
        public string Category { get; set; }
        public string Details { get; set; }
        public string Path { get; set; }
        public string FunctionName { get; set; }
        public CodeSmellRangeModel Range { get; set; }
        public CodeSmellRangeModel FunctionRange { get; set; } = null;
    }
}
