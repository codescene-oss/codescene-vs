using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class ApplyPayload
{
    public string Code { get; set; }
    public FnModel Fn { get; set; }
    public string Source { get; set; }
    public string FilePath { get; set; }
    public CodeRangeModel Range { get; set; }
}
