using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Community.VisualStudio.Toolkit;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Util
{
    public static class AceUtils
    {
        /// <summary>
        /// Checks if a file contains refactorable functions
        /// </summary>
        public static async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsAsync(FileReviewModel result, string code)
        {
            var aceRefactorService = await VS.GetMefServiceAsync<IAceRefactorService>();
            return aceRefactorService.CheckContainsRefactorableFunctions(result, code);
        }

        /// <summary>
        /// Finds the refactorable function matching a code smell.
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
