using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text;
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

        public static async Task<FnToRefactorModel> GetRefactorableFunctionAsync(GetRefactorableFunctionsModel model)
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();

            var preflight = preflightManager.GetPreflightResponse();

            if (model.FunctionRange == null) return null;

            var codeSmell = new CliCodeSmellModel()
            {
                Details = model.Details,
                Category = model.Category,
                Range = new Core.Models.Cli.CliRangeModel()
                {
                    StartColumn = model.FunctionRange.StartColumn,
                    EndColumn = model.FunctionRange.EndColumn,
                    Startline = model.FunctionRange.StartLine,
                    EndLine = model.FunctionRange.EndLine,
                },
            };

            // Get the current code snapshot from the document
            string fileContent = "";
            var docView = await VS.Documents.OpenAsync(model.Path);
            if (docView?.TextBuffer is ITextBuffer buffer)
            {
                fileContent = buffer.CurrentSnapshot.GetText();
            }

            var refactorableFunctions = aceManager.GetRefactorableFunctions(model.Path, fileContent, [codeSmell], preflight);
            return refactorableFunctions?.FirstOrDefault();
        }
    }
}
