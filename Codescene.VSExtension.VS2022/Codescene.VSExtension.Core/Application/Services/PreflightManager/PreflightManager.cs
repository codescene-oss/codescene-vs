using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.PreflightManager
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
            _logger.Debug($"Running preflight with force {force}");
            PreFlightResponseModel response = _executer.Preflight(force);

            if (response != null)
            {
                _logger.Info("Got preflight response. ACE service is active.");
                _preflightResponse = response;
                return response;
            } else
            {
                _logger.Info("Problem getting preflight response. ACE service is down.");
                _preflightResponse = null;
                return null;
            }
        }
        public decimal GetVersion() => _preflightResponse?.Version ?? 0;

        public bool IsAnyCodeSmellSupported(IEnumerable<string> codeSmells) => codeSmells.Intersect(_preflightResponse?.LanguageCommon.CodeSmells.ToList()).Any();

        public bool IsSupportedCodeSmell(string codeSmell) => _preflightResponse?.LanguageCommon.CodeSmells.Contains(codeSmell) ?? false;

        public bool IsSupportedLanguage(string extension) => _preflightResponse?.FileTypes.Contains(extension.Replace(".", "").ToLower()) ?? false;

        public bool IsSupportedLanguageAndCodeSmell(string extenison, string codeSmell) => IsSupportedLanguage(extenison) && IsSupportedCodeSmell(codeSmell);

        public PreFlightResponseModel GetPreflightResponse()
        {
            if (_preflightResponse == null)
            {
                return RunPreflight(true);
            } else
            {
                return _preflightResponse;
            }
        }
    }
}
