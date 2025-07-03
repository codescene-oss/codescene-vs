using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

class GotoFunctionLocationPayload
{
    public string FileName { get; set; }
    public FnData Fn { get; set; }
}

public class FnData
{
    public string Name { get; set; }
    public CodeSmellRangeModel Range { get; set; }
}

