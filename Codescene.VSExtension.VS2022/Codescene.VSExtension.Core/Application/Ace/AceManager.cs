using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.WebComponent.Model;

namespace Codescene.VSExtension.Core.Application.Ace
{
    [Export(typeof(IAceManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceManager : IAceManager
    {
        private readonly ILogger _logger;
        private readonly ICliExecutor _executor;
        private readonly ITelemetryManager _telemetryManager;
        private readonly IAceStateService _aceStateService;
        private readonly INetworkService _networkService;

        [ImportingConstructor]
        public AceManager(ILogger logger, ICliExecutor executor, ITelemetryManager telemetryManager, IAceStateService aceStateService, INetworkService networkService)
        {
            _logger = logger;
            _executor = executor;
            _telemetryManager = telemetryManager;
            _aceStateService = aceStateService;
            _networkService = networkService;
        }

        public static CachedRefactoringActionModel LastRefactoring;

        public IList<FnToRefactorModel> GetRefactorableFunctionsFromCodeSmells(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight)
        {
            return _executor.FnsToRefactorFromCodeSmells(fileName, fileContent, codeSmells, preflight);
        }

        public IList<FnToRefactorModel> GetRefactorableFunctionsFromDelta(string fileName, string fileContent, DeltaResponseModel deltaResponse, PreFlightResponseModel preflight)
        {
            return _executor.FnsToRefactorFromDelta(fileName, fileContent, deltaResponse, preflight);
        }

        public CachedRefactoringActionModel Refactor(string path, FnToRefactorModel refactorableFunction, string entryPoint, bool invalidateCache = false)
        {
            _logger.Info($"Starting refactoring of function: {refactorableFunction.Name} in file: {path}");

            // Check network connectivity before proceeding
            if (!_networkService.IsNetworkAvailable())
            {
                _logger.Warn("No internet connection available. Refactoring requires network access.");
                _aceStateService.SetState(AceState.Offline);
                LastRefactoring = null;
                return null;
            }

            SendTelemetry(entryPoint, invalidateCache);

            try
            {
                var refactoredFunction = _executor.PostRefactoring(fnToRefactor: refactorableFunction);

                if (refactoredFunction != null)
                {
                    _logger.Info($"Refactoring function: {refactorableFunction.Name}...");
                    _logger.Debug($"Refactoring trace-id: {refactoredFunction.TraceId}.");

                    // Clear any previous errors on success (matching VSCode behavior)
                    _aceStateService.ClearError();

                    // If we were offline, we're back online
                    if (_aceStateService.CurrentState == AceState.Offline)
                    {
                        _aceStateService.SetState(AceState.Enabled);
                    }

                    var cacheItem = new CachedRefactoringActionModel
                    {
                        Path = path,
                        RefactorableCandidate = refactorableFunction,
                        Refactored = refactoredFunction,
                    };
                    LastRefactoring = cacheItem;

                    return cacheItem;
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Error during refactoring of method {refactorableFunction.Name}", e);

                _aceStateService.SetError(e);

                throw;
            }

            LastRefactoring = null;
            return null;
        }

        public CachedRefactoringActionModel GetCachedRefactoredCode()
        {
            return LastRefactoring;
        }

        private void SendTelemetry(string entryPoint, bool invalidateCache)
        {
            Task.Run(() =>
            {
                var additionalData = new Dictionary<string, object>
                {
                    { "source", entryPoint },
                    { "skipCache", invalidateCache },
                };

                _telemetryManager.SendTelemetry(Constants.Telemetry.ACEREFACTORREQUESTED, additionalData);
            });
        }
    }
}
