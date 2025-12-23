using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class RetryPayload
{
    public FnToRefactorModel FnToRefactor { get; set; }
    public string Source { get; set; }
    public string FilePath { get; set; }
}