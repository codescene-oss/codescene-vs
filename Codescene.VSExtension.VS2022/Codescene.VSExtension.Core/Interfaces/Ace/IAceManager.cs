// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.WebComponent.Model;

namespace Codescene.VSExtension.Core.Interfaces.Ace
{
    public interface IAceManager
    {
        Task<CachedRefactoringActionModel> RefactorAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint, bool invalidateCache = false);

        CachedRefactoringActionModel GetCachedRefactoredCode();

        Task<IList<FnToRefactorModel>> GetRefactorableFunctionsFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResponse, PreFlightResponseModel preflight);

        Task<IList<FnToRefactorModel>> GetRefactorableFunctionsFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight);
    }
}
