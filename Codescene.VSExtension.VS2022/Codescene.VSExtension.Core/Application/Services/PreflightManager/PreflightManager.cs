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
        private string[] _codeSmells;
        private string[] _languages;
        private decimal _version;

        public async Task<PreFlightResponseModel> RunPreflightAsync(bool force = false)
        {
            _logger.Info($"Running preflight with force {force}");
            PreFlightResponseModel response = null;
            await Task.Run(() =>
            {
                response = _executer.Preflight(force);
                return Task.CompletedTask;
            });

            if (response != null)
            {
                _logger.Info("Got preflight response");
            }
            _preflightResponse = response;
            _version = response.Version;
            _codeSmells = response.LanguageCommon.CodeSmells;
            _languages = response.FileTypes;
            return response;
        }
        public decimal GetVersion() => _version;

        public bool IsAnyCodeSmellSupported(IEnumerable<string> codeSmells) => codeSmells.Intersect(_codeSmells.ToList()).Any();

        public bool IsSupportedCodeSmell(string codeSmell) => _codeSmells.Contains(codeSmell);

        public bool IsSupportedLanguage(string extension) => _languages.Contains(extension.Replace(".", "").ToLower());

        public bool IsSupportedLanguageAndCodeSmell(string extenison, string codeSmell) => IsSupportedLanguage(extenison) && IsSupportedCodeSmell(codeSmell);

        public PreFlightResponseModel GetPreflightResponse()
        {
            if (_preflightResponse == null)
            {
                return RunPreflightAsync(true).GetAwaiter().GetResult();
            } else
            {
                return _preflightResponse;
            }
        }
    }
}
