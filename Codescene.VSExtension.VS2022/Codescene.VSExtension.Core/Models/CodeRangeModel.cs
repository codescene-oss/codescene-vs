namespace Codescene.VSExtension.Core.Models
{
    public class CodeRangeModel
    {
        public int StartLine { get; set; }

        public int EndLine { get; set; }

        public int StartColumn { get; set; }

        public int EndColumn { get; set; }

        public CodeRangeModel(int startLine, int endLine, int startColumn, int endColumn)
        {
            StartLine = startLine;
            EndLine = endLine;
            StartColumn = startColumn;
            EndColumn = endColumn;
        }
    }
}
