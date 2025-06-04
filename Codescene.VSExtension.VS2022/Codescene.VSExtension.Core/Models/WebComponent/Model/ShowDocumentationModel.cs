namespace Codescene.VSExtension.Core.Models.WebComponent.Model
{
    public class ShowDocumentationModel
    {
        public string Path { get; set; }
        public string Category { get; set; }
        public string FunctionName { get; set; }
        public CodeSmellRange Range { get; set; }

        public ShowDocumentationModel(string path, string category, string functionName, CodeSmellRange range)
        {
            Path = path;
            Category = category;
            FunctionName = functionName;
            Range = range;
        }
    }

    public class CodeSmellRange
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }

        public CodeSmellRange(int startLine, int endLine, int startColumn, int endColumn)
        {
            StartLine = startLine;
            EndLine = endLine;
            StartColumn = startColumn;
            EndColumn = endColumn;
        }
    }
}
