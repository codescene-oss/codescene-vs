using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class CachedRefactoringActionModel
    {
        public string Path { get; set; }
        public RefactorResponseModel Refactored { get; set; }
        public FnToRefactorModel RefactorableCandidate { get; set; }
    }
}
