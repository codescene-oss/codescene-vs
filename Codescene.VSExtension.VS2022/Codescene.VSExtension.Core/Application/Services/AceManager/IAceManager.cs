using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.AceManager
{
    public interface IAceManager
    {
        CachedRefactoringActionModel Refactor(string path, FnToRefactorModel refactorableFunction, string entryPoint, bool invalidateCache = false);
        CachedRefactoringActionModel GetCachedRefactoredCode();
        IList<FnToRefactorModel> GetRefactorableFunctions(string content, string codesmellsJson, string preflight, string fileName);
        IList<FnToRefactorModel> GetRefactorableFunctionsFromDelta(string content, string deltaJson, string preflight, string fileName);
    }
}