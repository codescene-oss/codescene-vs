using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.VS2022.Util;

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

        [Import]
        private readonly ITelemetryManager _telemetryManager;

        public static CachedRefactoringActionModel LastRefactoring;

        public IList<FnToRefactorModel> GetRefactorableFunctions(string content, string codesmellsJson, string preflight, string extension)
        {
            return _executer.FnsToRefactorFromCodeSmells(content, extension, codesmellsJson, preflight);
        }

        public CachedRefactoringActionModel Refactor(string path, FnToRefactorModel refactorableFunction, string entryPoint, bool invalidateCache = false)
        {
            _logger.Info($"Starting refactoring of function: {refactorableFunction.Name} in file: {path}");
            SendTelemetry(entryPoint);

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

            try
            {
                var refactoredFunctions = _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString);

                if (refactoredFunctions != null)
                {
                    _logger.Info($"Refactored function: {refactorableFunction.Name}");
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
            }
            catch (Exception e)
            {
                _logger.Error($"Error during refactoring of method {functionForRefactor.Name}", e);
            }
            return null;
        }

        public CachedRefactoringActionModel GetCachedRefactoredCode()
        {
            return LastRefactoring;
        }

        private void SendTelemetry(string entryPoint)
        {
            Task.Run(() =>
            {
                var additionalData = new Dictionary<string, object>
                {
                    { "source", entryPoint },
                    { "skipCache", false }
                };

                _telemetryManager.SendTelemetry(Constants.Telemetry.ACE_REFACTOR_REQUESTED, additionalData);
            });
        }
    }
}