using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class CopyCodePayload
{
    public FnModel Fn { get; set; }
    public string Code { get; set; }
    public string Source { get; set; }
    public string FilePath { get; set; }
    public CodeRangeModel Range { get; set; }
}

public class FnModel
{
    public string Name { get; set; }
    public CodeRangeModel Range { get; set; }
}