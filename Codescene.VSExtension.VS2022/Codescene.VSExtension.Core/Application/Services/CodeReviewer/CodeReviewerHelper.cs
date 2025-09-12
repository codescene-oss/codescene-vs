using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public static class CodeReviewerHelper
    {
        public static void UpdateDeltaCacheWithRefactorableFunctions(DeltaResponseModel delta, string path, string code, ILogger logger)
        {
            var cacheService = new AceRefactorableFunctionsCacheService();
            var refactorableFunctions = cacheService.Get(new AceRefactorableFunctionsQuery(path, code));

            logger.Debug($"Updating delta cache with refactorable functions for {path}. Found {refactorableFunctions.Count} refactorable functions.");

            if (ShouldSkipUpdate(delta, refactorableFunctions, logger))
            {
                return;
            }

            UpdateFindings(delta, refactorableFunctions);
        }

        public static bool ShouldSkipUpdate(DeltaResponseModel delta, IList<FnToRefactorModel> refactorableFunctions, ILogger logger)
        {
            if (delta == null)
            {
                logger.Debug("Delta response null. Skipping update of delta cache.");
                return true;
            }
            if (!refactorableFunctions.Any())
            {
                logger.Debug("No refactorable functions found. Skipping update of delta cache.");
                return true;
            }
            return false;
        }

        public static void UpdateFindings(DeltaResponseModel delta, IList<FnToRefactorModel> refactorableFunctions)
        {
            foreach (var finding in delta.FunctionLevelFindings)
            {
                var functionName = finding.Function?.Name;
                if (string.IsNullOrEmpty(functionName))
                    continue;

                UpdateFindingIfNotUpdated(finding, functionName, refactorableFunctions);
            }
        }

        public static void UpdateFindingIfNotUpdated(FunctionFindingModel finding, string functionName, IList<FnToRefactorModel> refactorableFunctions)
        {
            // update only if not already updated, for case when multiple methods have same name
            if (finding.RefactorableFn == null)
            {
                var match = refactorableFunctions.FirstOrDefault(fn => fn.Name == functionName && CheckRange(finding, fn));
                if (match != null)
                {
                    finding.RefactorableFn = match;
                }
            }
        }

        public static bool CheckRange(FunctionFindingModel finding, FnToRefactorModel refFunction)
        {
            // this check is because of ComplexConditional code smell which is inside of the method
            return refFunction.Range.Startline <= finding.Function.Range.Startline &&
                finding.Function.Range.Startline <= refFunction.Range.EndLine;
        }
    }
}