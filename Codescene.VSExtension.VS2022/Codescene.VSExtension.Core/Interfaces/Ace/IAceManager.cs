using System.Collections.Generic;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.WebComponent.Model;

namespace Codescene.VSExtension.Core.Interfaces.Ace
{
    public interface IAceManager
    {
        CachedRefactoringActionModel Refactor(string path, FnToRefactorModel refactorableFunction, string entryPoint, bool invalidateCache = false);

        CachedRefactoringActionModel GetCachedRefactoredCode();

        IList<FnToRefactorModel> GetRefactorableFunctionsFromDelta(string fileName, string fileContent, DeltaResponseModel deltaResponse, PreFlightResponseModel preflight);

        IList<FnToRefactorModel> GetRefactorableFunctionsFromCodeSmells(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight);
    }
}
