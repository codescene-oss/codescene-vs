namespace Codescene.VSExtension.Core.Models.WebComponent.Model
{
    public class ShowDocumentationModel
    {
        public string Path { get; set; }
        public string Category { get; set; }
        public string FunctionName { get; set; }
        public CodeRangeModel Range { get; set; }

        public ShowDocumentationModel(string path, string category, string functionName, CodeRangeModel range)
        {
            Path = path;
            Category = category;
            FunctionName = functionName;
            Range = range;
        }
    }
}
