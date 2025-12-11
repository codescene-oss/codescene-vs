namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class CodeSmellDocumentationComponentData
    {
        public string DocType { get; set; }
        public AutoRefactorConfig AutoRefactor { get; set; }
        public FileDataModel FileData { get; set; }
    }

    public class FileDataModel
    {
        public string FileName { get; set; }
        public FunctionModel Fn { get; set; }

    }

    public class FunctionModel
    {
        public string Name { get; set; }
        public CodeSmellRangeModel Range { get; set; }
    }
}
