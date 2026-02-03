namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class OpenDocsForFunctionPayload
{
    public string FileName { get; set; }

    public FnData Fn { get; set; }

    public string DocType { get; set; }
}
