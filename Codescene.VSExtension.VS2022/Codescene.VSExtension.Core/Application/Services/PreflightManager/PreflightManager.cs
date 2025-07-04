using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.PreflightManager
{
    [Export(typeof(IPreflightManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PreflightManager : IPreflightManager
    {
        [Import]
        private readonly ICliExecutor _executer;

        private PreFlightResponseModel _preflightResponse;
        private string[] _codeSmells;
        private string[] _languages;
        private decimal _version;

        //public PreflightManager(ICliExecutor executer)
        //{
        //    _executer = executer;
        //    var response = _executer.Preflight(); // no need to force the response, it is already cached
        //    _preflightResponse = response;
        //    _version = response.Version;
        //    _codeSmells = response.LanguageCommon.CodeSmells;
        //    _languages = response.FileTypes;
        //}

        public PreFlightResponseModel RunPreflight(bool force = false)
        {
            var response = _executer.Preflight(force); // no need to force the response, it is already cached
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
            return _preflightResponse;
        }
    }
}
