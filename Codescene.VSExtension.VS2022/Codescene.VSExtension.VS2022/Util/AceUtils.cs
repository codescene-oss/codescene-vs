using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Util
{
    public class AceUtils
    {
        private static readonly AceRefactorableFunctionsCacheService _cacheService = new();
        public static async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsAsync(FileReviewModel result)
        {
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var mapper = await VS.GetMefServiceAsync<IModelMapper>();
            var logger = await VS.GetMefServiceAsync<ILogger>();

            // Validate required services
            if (aceManager == null || preflightManager == null || mapper == null || logger == null)
            {
                return new List<FnToRefactorModel>();
            }

            preflightManager.RunPreflight();

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

            logger.Info("Dzenan 1");

            if (!ShouldCheckRefactorableFunctions(extension, preflightManager))
            {
                logger.Info("Dzenan 1.1");
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
            var preflight = JsonConvert.SerializeObject(preflightManager.RunPreflight());

            logger.Info("Dzenan 2");

            try
            {
                using (var reader = File.OpenText(path))
                {
                    var content = await reader.ReadToEndAsync();
                    var refactorableFunctions = await aceManager.GetRefactorableFunctions(content, codesmellsJson, preflight, extension);

                    logger.Info("Dzenan 3");

                    if (refactorableFunctions.Any())
                    {
                        logger.Info($"Found {refactorableFunctions.Count} refactorable function(s) in path {path}");
                        
                        var cacheEntry = new AceRefactorableFunctionsEntry(path, content, refactorableFunctions);
                        _cacheService.Put(cacheEntry);
                        
                        return refactorableFunctions;
                    }
                    else
                    {
                        logger.Warn($"No refactorable functions found for path: {path}");
                        return [];
                    }

                    logger.Info("Dzenan 4");
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

        private static bool ShouldCheckRefactorableFunctions(string extension, IPreflightManager preflightManager)
        {
            var state = General.Instance.EnableAutoRefactor;
            if (!state)
                return false;

            return preflightManager.IsSupportedLanguage(extension);
        }
    }
}
