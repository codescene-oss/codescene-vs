namespace Codescene.VSExtension.Core.Models
{
    public class CodeSmellModel
    {
        public string Path { get; set; }
        public string Category { get; set; }
        public string Details { get; set; }
        public string FunctionName { get; set; }
        public CodeSmellRangeModel Range { get; set; }
    }
}
