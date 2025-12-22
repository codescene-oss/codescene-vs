using Codescene.VSExtension.Core.Models.Cli;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class ApplyPayload
{
    public string Code { get; set; }
    public FnModel Fn { get; set; }
    public string Source { get; set; }
    public string FilePath { get; set; }
    public CliRangeModel Range { get; set; }
}