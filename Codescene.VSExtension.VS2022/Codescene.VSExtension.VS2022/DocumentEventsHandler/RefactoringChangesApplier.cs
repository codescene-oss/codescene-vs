using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(RefactoringChangesApplier))]
[PartCreationPolicy(CreationPolicy.Shared)]

public class RefactoringChangesApplier
{
    public void Apply()
    {
        var e = string.Empty;
    }
}
