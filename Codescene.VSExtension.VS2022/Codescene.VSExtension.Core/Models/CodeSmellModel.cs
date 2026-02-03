namespace Codescene.VSExtension.Core.Models
{
    public class CodeSmellModel
    {
        public string Path { get; set; }
        public string Category { get; set; }
        public string Details { get; set; }
        public string FunctionName { get; set; }
        public CodeRangeModel Range { get; set; }
        /// <summary>
        /// The range of the function that contains this code smell.
        /// This is only set for function-level code smells.
        /// </summary>
        public CodeRangeModel FunctionRange { get; set; }
    }
}
