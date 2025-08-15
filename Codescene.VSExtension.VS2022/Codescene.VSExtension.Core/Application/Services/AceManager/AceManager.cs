using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
        private readonly ICliExecutor _executer;

        public static CachedRefactoringActionModel LastRefactoring;

        public async Task<IList<FnToRefactorModel>> GetRefactorableFunctions(string content, string codesmellsJson, string preflight, string extension)
        {
            //_logger.Info($"Calling GetRefactorableFunctions with arguments: content-{content}, codesmellsJson-{codesmellsJson}, preflight-{preflight}, extension-{extension}");
            return await _executer.FnsToRefactorFromCodeSmellsAsync(content, extension, codesmellsJson, preflight);
        }

        public async Task<CachedRefactoringActionModel> Refactor(string path, FnToRefactorModel refactorableFunction, bool invalidateCache = false)
        {
            var rangeList = new List<PositionModel> {
                new PositionModel
                {
                    Line = refactorableFunction.Range.Startline - 1,
                    Character = refactorableFunction.Range.StartColumn - 1
                },
                new PositionModel
                {
                    Line = refactorableFunction.Range.EndLine - 1,
                    Character = refactorableFunction.Range.EndColumn - 1
                }
            };

            var functionForRefactor = new FnToRefactorModel
            {
                Name = refactorableFunction.Name,
                Body = refactorableFunction.Body,
                FileType = refactorableFunction.FileType,
                Range = refactorableFunction.Range,
                RefactoringTargets = refactorableFunction.RefactoringTargets,
                VSCodeRange = rangeList.ToArray()
            };

            var refactorableFunctionsString = JsonConvert.SerializeObject(functionForRefactor);

            var refactoredFunctions = await _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString);

            if (refactoredFunctions == null)
            {
                throw new Exception($"Refactoring has failed! Credits left: {refactoredFunctions.CreditsInfo.Limit - refactoredFunctions.CreditsInfo.Used}.");
            }

            _logger.Info($"Refactored function: {refactorableFunction.Name} in file: {path}");
            _logger.Debug($"Refactoring trace-id: {refactoredFunctions.TraceId}, credits left: {refactoredFunctions.CreditsInfo.Limit - refactoredFunctions.CreditsInfo.Used}.");

            var cacheItem = new CachedRefactoringActionModel
            {
                Path = path,
                RefactorableCandidate = functionForRefactor,
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