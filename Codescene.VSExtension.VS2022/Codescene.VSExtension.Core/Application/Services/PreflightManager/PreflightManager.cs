using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.PreflightManager
{
    [Export(typeof(IPreflightManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PreflightManager : IPreflightManager
    {
        private readonly ICliExecutor _executer;
        private readonly ILogger _logger;

        private PreFlightResponseModel _preflightResponse;
        private AutoRefactorConfig _autoRefactorConfig;

        [ImportingConstructor]
        public PreflightManager(ICliExecutor executer, ILogger logger)
        {
            _executer = executer;
            _logger = logger;
        }

        public PreFlightResponseModel RunPreflight(bool force = false)
        {
            _logger.Debug($"Running preflight with force: {force}");

#if FEATURE_ACE
            var response = _executer.Preflight(force);

            if (response != null)
            {
                _logger.Info("Got preflight response. ACE service is active.");
                _preflightResponse = response;
                _autoRefactorConfig = new() { Activated = true, Visible = true, Disabled = false };
                return response;
            }
            else
            {
                _logger.Info("Problem getting preflight response. ACE service is down.");
                _preflightResponse = null;
                _autoRefactorConfig = new() { Activated = true, Visible = true, Disabled = false };
            }
#endif

            return null;
        }

        public bool IsSupportedLanguage(string extension) => _preflightResponse?.FileTypes.Contains(extension.Replace(".", "").ToLower()) ?? false;

        public PreFlightResponseModel GetPreflightResponse()
        {
            if (_preflightResponse == null) return RunPreflight(true);

            return _preflightResponse;
        }

        public AutoRefactorConfig GetAutoRefactorConfig() => _autoRefactorConfig ?? new() { Activated = true, Visible = true, Disabled = false };
    }
}
