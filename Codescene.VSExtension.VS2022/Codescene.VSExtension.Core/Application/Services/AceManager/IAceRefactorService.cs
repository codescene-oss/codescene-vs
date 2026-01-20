using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.AceManager
{
    public interface IAceRefactorService
    {
        /// <summary>
        /// Checks if a file contains refactorable functions based on its code smells.
        /// </summary>
        IList<FnToRefactorModel> CheckContainsRefactorableFunctions(FileReviewModel result, string code);

        /// <summary>
        /// Finds the refactorable function that matches a specific code smell.
        /// </summary>
        FnToRefactorModel GetRefactorableFunction(CodeSmellModel codeSmell, IList<FnToRefactorModel> refactorableFunctions);

        /// <summary>
        /// Determines if refactorable functions should be checked for the given file extension.
        /// </summary>
        bool ShouldCheckRefactorableFunctions(string extension);
    }
}
