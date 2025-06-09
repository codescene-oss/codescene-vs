namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class CodeSmellDocumentationComponentData
    {
        public string DocType { get; set; }
        public AutoRefactorModel AutoRefactor { get; set; }
        public FileDataModel FileData { get; set; }
    }

    public class AutoRefactorModel
    {
        public bool Activated { get; set; }
        public bool Disabled { get; set; }
        public bool Visible { get; set; }
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
