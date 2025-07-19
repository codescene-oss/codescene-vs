using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.AceManager
{
    [Export(typeof(IAceManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceManager : IAceManager
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliExecutor _executer;

        [Import]
        private readonly IPreflightManager _preflightManager;

        public static CachedRefactoringActionModel LastRefactoring;

        public async Task<IList<FnToRefactorModel>> GetRefactorableFunctions(string content, string codesmellsJson, string preflight, string extension)
        {
            //_logger.Info($"Calling GetRefactorableFunctions with arguments: content-{content}, codesmellsJson-{codesmellsJson}, preflight-{preflight}, extension-{extension}");
            return await _executer.FnsToRefactorFromCodeSmellsAsync(content, extension, codesmellsJson, preflight);
        }

        public async Task<CachedRefactoringActionModel> Refactor(string path, FnToRefactorModel refactorableFunction, bool invalidateCache = false)
        {
            if (string.IsNullOrWhiteSpace(refactorableFunction.FunctionType))
            {
                refactorableFunction.FunctionType = "MemberFn";
            }

            var refactorableFunctionsString = JsonConvert.SerializeObject(refactorableFunction);

            var refactoredFunctions = await _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString, skipCache: false);

            if (refactoredFunctions == null)
            {
                throw new Exception("Refactoring has failed!");
            }

            var cacheItem = new CachedRefactoringActionModel
            {
                Path = path,
                RefactorableCandidate = refactorableFunction,
                Refactored = refactoredFunctions
            };

            LastRefactoring = cacheItem;

            return cacheItem;
        }

        public CachedRefactoringActionModel GetCachedRefactoredCode()
        {
            return LastRefactoring;
        }
    }
}