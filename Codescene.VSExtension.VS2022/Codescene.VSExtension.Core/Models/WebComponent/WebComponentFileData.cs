namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class WebComponentFileData
    {
        public string Filename { get; set; }
        public string FunctionName { get; set; }
        public int LineNumber { get; set; }
        public WebComponentAction Action { get; set; }
    }

    public class WebComponentAction
    {
        public string GoToFunctionLocationPayload { get; set; }
    }
}
