using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.VS2022.Application.Services.PreflightManager
{
    [Export(typeof(IPreflightManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PreflightManager : IPreflightManager
    {
        [Import]
        private readonly ICliExecutor _executer;

        [Import]
        private readonly ILogger _logger;

        private PreFlightResponseModel _preflightResponse;

        public PreFlightResponseModel RunPreflight(bool force = false)
        {
			_logger.Debug($"Running preflight with force: {force}");
			var aceEnabled = General.Instance.EnableAutoRefactor;
			if (!aceEnabled)
			{
				_logger.Debug("Auto refactor is disabled in options.");
				return null;
			} 
            else
            {
				var response = _executer.Preflight(force);

				if (response != null)
				{
					_logger.Info("Got preflight response. ACE service is active.");
					_preflightResponse = response;
					return response;
				}
				else
				{
					_logger.Info("Problem getting preflight response. ACE service is down.");
					_preflightResponse = null;
					return null;
				}
			}
        }

        public bool IsSupportedLanguage(string extension) => _preflightResponse?.FileTypes.Contains(extension.Replace(".", "").ToLower()) ?? false;

        public PreFlightResponseModel GetPreflightResponse()
        {
            if (_preflightResponse == null) return RunPreflight(true);
            
            return _preflightResponse;
        }
    }
}