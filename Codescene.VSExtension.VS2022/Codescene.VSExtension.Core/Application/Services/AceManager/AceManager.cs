using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
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
        private readonly ICliExecutor _executor;

        [Import]
        private readonly ITelemetryManager _telemetryManager;

        public static CachedRefactoringActionModel LastRefactoring;

        public IList<FnToRefactorModel> GetRefactorableFunctions(string content, string codesmellsJson, string preflight, string extension)
        {
            return _executor.FnsToRefactorFromCodeSmells(content, extension, codesmellsJson, preflight);
        }

        public CachedRefactoringActionModel Refactor(string path, FnToRefactorModel refactorableFunction, string entryPoint, bool invalidateCache = false)
        {
            _logger.Info($"Starting refactoring of function: {refactorableFunction.Name} in file: {path}");
            
            // Check network connectivity before proceeding
            if (!IsNetworkAvailable())
            {
                _logger.Warn("No internet connection available. Refactoring requires network access.");
				LastRefactoring = null;
				return null;
            }
            
            SendTelemetry(entryPoint, invalidateCache);

			try
            {
                var refactoredFunction = _executor.PostRefactoring(fnToRefactor: refactorableFunction);

                if (refactoredFunction != null)
                {
                    _logger.Info($"Refactored function: {refactorableFunction.Name}");
                    _logger.Debug($"Refactoring trace-id: {refactoredFunction.TraceId}.");

                    var cacheItem = new CachedRefactoringActionModel
                    {
                        Path = path,
                        RefactorableCandidate = refactorableFunction,
                        Refactored = refactoredFunction
                    };
                    LastRefactoring = cacheItem;

                    return cacheItem;
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Error during refactoring of method {refactorableFunction.Name}", e);
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
                    { "skipCache", invalidateCache }
                };

                _telemetryManager.SendTelemetry(Constants.Telemetry.ACE_REFACTOR_REQUESTED, additionalData);
            });
        }

        private bool IsNetworkAvailable()
        {
            // Implementation to check network connectivity
            return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        }
    }
}