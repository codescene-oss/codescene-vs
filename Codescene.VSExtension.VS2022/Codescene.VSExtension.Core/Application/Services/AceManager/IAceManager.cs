using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.AceManager
{
    public interface IAceManager
    {
        CachedRefactoringActionModel Refactor(string path, FnToRefactorModel refactorableFunction, string entryPoint, bool invalidateCache = false);
        CachedRefactoringActionModel GetCachedRefactoredCode();
        IList<FnToRefactorModel> GetRefactorableFunctions(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight);
    }
}