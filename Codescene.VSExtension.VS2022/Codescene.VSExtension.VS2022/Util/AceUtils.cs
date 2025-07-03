using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;
using EnvDTE;
using EnvDTE80;
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
        public static async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsAsync(FileReviewModel result)
        {

            var executor = await ServiceProvider.GetGlobalServiceAsync(typeof(ICliExecutor)) as ICliExecutor;
            var aceManager = await ServiceProvider.GetGlobalServiceAsync(typeof(IAceManager)) as IAceManager;
            var preflightManager = new PreflightManager(executor);

            var path = result.FilePath;
            var codesmellsJson = JsonConvert.SerializeObject(result.FunctionLevel.Concat(result.FileLevel).ToList());

            var preflight = JsonConvert.SerializeObject(preflightManager.GetPreflightResponse());
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
                    return await aceManager.GetRefactorableFunctions(content, codesmellsJson, preflight, extension);
                }
            }
            return [];
        }

        private static bool ShouldCheckRefactorableFunctions(string extension, PreflightManager preflightManager)
        {
            var state = General.Instance.EnableAutoRefactor;
            if (!state)
                return false;

            return preflightManager.IsSupportedLanguage(extension);
        }
    }
}
