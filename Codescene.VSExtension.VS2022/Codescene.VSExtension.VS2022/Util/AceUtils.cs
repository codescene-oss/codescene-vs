using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Community.VisualStudio.Toolkit;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Util
{
    /// <summary>
    /// Thin wrapper for ACE refactoring utilities.
    /// Delegates to IAceRefactorService for testable business logic.
    /// </summary>
    public static class AceUtils
    {
        /// <summary>
        /// Checks if a file contains refactorable functions. Delegates to IAceRefactorService.
        /// </summary>
        public static async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsAsync(FileReviewModel result, string code)
        {
#if FEATURE_ACE
            var aceRefactorService = await VS.GetMefServiceAsync<IAceRefactorService>();
            return aceRefactorService.CheckContainsRefactorableFunctions(result, code);
#else
            return new List<FnToRefactorModel>();
#endif
        }

        /// <summary>
        /// Finds the refactorable function matching a code smell.
        /// Pure logic - kept as static helper for non-MEF classes like ReviewResultTagger.
        /// </summary>
        public static FnToRefactorModel GetRefactorableFunction(CodeSmellModel codeSmell, IList<FnToRefactorModel> refactorableFunctions)
        {
            return refactorableFunctions.FirstOrDefault(function =>
                function.RefactoringTargets.Any(target =>
                    target.Category == codeSmell.Category &&
                    target.Line == codeSmell.Range.StartLine
                )
            );
        }
    }
}
