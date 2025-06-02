namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class ShowDocsMessage
    {
        public string MessageType { get; set; }
        public ShowDocsPayload Payload { get; set; }
    }

    public class ShowDocsPayload
    {
        public string IdeType { get; set; }
        public string View { get; set; }
        public ShowDocsModel Data { get; set; }
    }

    public class ShowDocsModel
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
        public ActionModel Action { get; set; }

    }

    public class ActionModel
    {
        public GoToFunctionLocationPayloadModel GoToFunctionLocationPayload { get; set; }
    }

    public class RangeModel
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
    }

    public class GoToFunctionLocationPayloadModel
    {
        public string FileName { get; set; }
        public FunctionModel Fn { get; set; }
    }

    public class FunctionModel
    {
        public string Name { get; set; }
        public RangeModel Range { get; set; }
    }
}
