using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class RequestAndPresentRefactoringPayload
{
    public FnData Fn { get; set; }

    public string Source { get; set; }

    public string FileName { get; set; }

    public CodeRangeModel Range { get; set; }

    public FnToRefactorModel FnToRefactor { get; set; }
}
