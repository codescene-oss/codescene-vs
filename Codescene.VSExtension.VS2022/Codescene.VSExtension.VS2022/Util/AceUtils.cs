using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;
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
            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var executorService = componentModel.GetService<ICliExecutor>();
            var aceManager = componentModel.GetService<IAceManager>();
            var preflightManager = componentModel.GetService<IPreflightManager>();
            var mapper = componentModel.GetService<IModelMapper>();

            var path = result.FilePath;
            var codeSmellModelList = result.FunctionLevel.Concat(result.FileLevel);

            var cliCodeSmellModelList = new List<CliCodeSmellModel>();
            foreach (var codeSmellModel in codeSmellModelList)
            {
                var cliCodeSmellModel = mapper.Map(codeSmellModel);
                cliCodeSmellModelList.Add(cliCodeSmellModel);
            }

            var codesmellsJson = JsonConvert.SerializeObject(cliCodeSmellModelList);

            var preflight = JsonConvert.SerializeObject(preflightManager.RunPreflight());
            var fileName = Path.GetFileName(path);
            var extension = Path.GetExtension(fileName).Replace(".", "");

            if (ShouldCheckRefactorableFunctions(extension, preflightManager))
            {
                using (var reader = File.OpenText(path))
                {
                    var content = await reader.ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        // TODO: meaningful log
                    }
                    var refactorableFunctions = await aceManager.GetRefactorableFunctions(content, codesmellsJson, preflight, extension);
                    var cacheEntry = new AceRefactorableFunctionsEntry(path, content, refactorableFunctions);
                    _cacheService.Put(cacheEntry);
                    return refactorableFunctions;
                }
            }
            return [];
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
