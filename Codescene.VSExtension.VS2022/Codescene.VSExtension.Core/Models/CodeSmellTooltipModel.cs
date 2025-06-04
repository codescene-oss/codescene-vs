using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Models
{
    public class CodeSmellTooltipModel
    {
        public string Category { get; set; }
        public string Details { get; set; }
        public string Path { get; set; }
        public string FunctionName { get; set; }
        public RangeModel Range { get; set; }

        public static CodeSmellTooltipModel Create(string category, string details, string path, string functionName, RangeModel range)
        {
            return new CodeSmellTooltipModel
            {
                Category = category,
                Details = details,
                Path = path,
                FunctionName = functionName,
                Range = range
            };
        }
    }
}
