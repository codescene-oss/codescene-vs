using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models.Cli;
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
            //if (string.IsNullOrWhiteSpace(refactorableFunction.FunctionType))
            //{
            //    refactorableFunction.FunctionType = "MemberFn";
            //}

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
                RefactoringTargets = new RefactoringTargetModel[] { refactorableFunction.RefactoringTargets.First() },
                VSCodeRange = rangeList.ToArray()
            };



            var refactorableFunctionsString = JsonConvert.SerializeObject(functionForRefactor);

            var refactoredFunctions = await _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString);

            if (refactoredFunctions == null)
            {
                throw new Exception("Refactoring has failed!");
            }

            _logger.Info($"Refactored function: {refactorableFunction.Name} in file: {path}");
            _logger.Debug($"Refactoring trace-id: {refactoredFunctions.TraceId}");

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