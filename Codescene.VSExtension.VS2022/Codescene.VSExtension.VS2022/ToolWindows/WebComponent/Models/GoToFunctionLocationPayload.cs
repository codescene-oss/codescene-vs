namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

class GotoFunctionLocationPayload
{
    public string FileName { get; set; }
    public FnData Fn { get; set; }
}

internal class FnData
{
    public string Name { get; set; }
    public Range Range { get; set; }
}

internal class Range
{
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
}

