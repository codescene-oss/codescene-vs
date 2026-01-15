using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Util
{
    public static class AceUtils
    {
        private static readonly AceRefactorableFunctionsCacheService _cache = new();

        public static async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsAsync(FileReviewModel result, string code)
        {
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var mapper = await VS.GetMefServiceAsync<IModelMapper>();
            var logger = await VS.GetMefServiceAsync<ILogger>();

            preflightManager.GetPreflightResponse();

            var path = result.FilePath;
            var fileName = Path.GetFileName(path);

            logger.Info($"Checking if refactorable functions available for file {path}");

            // Check fileName before proceeding
            if (string.IsNullOrWhiteSpace(fileName))
            {
                logger.Warn($"Invalid file name for path: {path}");
                return new List<FnToRefactorModel>();
            }

            var extension = Path.GetExtension(fileName).Replace(".", "");

            if (!ShouldCheckRefactorableFunctions(extension, preflightManager, logger))
            {
                return new List<FnToRefactorModel>();
            }

            var codeSmellModelList = result.FunctionLevel.Concat(result.FileLevel);
            var cliCodeSmellModelList = new List<CliCodeSmellModel>();
            
            foreach (var codeSmellModel in codeSmellModelList)
            {
                var cliCodeSmellModel = mapper.Map(codeSmellModel);
                cliCodeSmellModelList.Add(cliCodeSmellModel);
            }

            var codesmellsJson = JsonConvert.SerializeObject(cliCodeSmellModelList);
            var preflight = JsonConvert.SerializeObject(preflightManager.GetPreflightResponse());

            try
            {
                var refactorableFunctions = aceManager.GetRefactorableFunctions(code, codesmellsJson, preflight, fileName);

                if (refactorableFunctions.Any())
                {
                    logger.Info($"Found {refactorableFunctions.Count} refactorable function(s) in path {path}");
                        
                    var cacheEntry = new AceRefactorableFunctionsEntry(path, code, refactorableFunctions);
                    logger.Debug($"Caching refactorable functions for path: {path}.");
                    _cache.Put(cacheEntry);
					return refactorableFunctions;
                }
                else
                {
                    logger.Warn($"No refactorable functions found for path: {path}");
                    return new List<FnToRefactorModel>();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking refactorable functions for path: {path}", ex);
                return new List<FnToRefactorModel>();
            }
        }

        public static FnToRefactorModel GetRefactorableFunction(CodeSmellModel codeSmell, IList<FnToRefactorModel> refactorableFunctions)
        {
            return refactorableFunctions.FirstOrDefault(function =>
                function.RefactoringTargets.Any(target =>
                    target.Category == codeSmell.Category &&
                    target.Line == codeSmell.Range.StartLine
                )
            );
        }

		private static bool ShouldCheckRefactorableFunctions(string extension, IPreflightManager preflightManager, ILogger logger)
        {
            if (preflightManager.IsSupportedLanguage(extension) == false)
            {
                logger.Debug($"Auto refactor is not supported for language: {extension}");
                return false;
            }
            return true;
        }

        public static async Task UpdateDeltaCacheWithRefactorableFunctions(DeltaResponseModel delta, string path, string code, ILogger logger)
        {
            //var cacheService = new AceRefactorableFunctionsCacheService();
            //var refactorableFunctions = cacheService.Get(new AceRefactorableFunctionsQuery(path, code));
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var deltaString = JsonConvert.SerializeObject(delta);
            var fileName = Path.GetFileName(path);
            var preflight = JsonConvert.SerializeObject(preflightManager.GetPreflightResponse());

            logger.Info($"Checking if refactorable functions from delta available for file {path}");

            // Check fileName before proceeding
            if (string.IsNullOrWhiteSpace(fileName))
            {
                logger.Warn($"Invalid file name for path: {path}");
                return;
            }

            var refactorableFunctions = aceManager.GetRefactorableFunctionsFromDelta(code, deltaString, preflight, fileName);

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
            // this check is because of ComplexConditional code smell which is inside of the method (only in js, maybe a bug in cli)
            return refFunction.Range.Startline <= finding.Function.Range.Startline &&
                finding.Function.Range.Startline <= refFunction.Range.EndLine;

            //return refFunction.Range.Startline == finding.Function.Range.Startline &&
            //	finding.Function.Range.EndLine == refFunction.Range.EndLine;
        }
    }
}
