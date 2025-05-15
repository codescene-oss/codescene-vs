using Codescene.VSExtension.Core.Application.Services.Cli;
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
        private readonly ICliExecuter _executer;

        private readonly string[] _codeSmells;
        private readonly string[] _languages;
        private readonly decimal _version;

        [ImportingConstructor]
        public PreflightManager(ICliExecuter executer)
        {
            _executer = executer;
            var response = _executer.Preflight(force: true);
            _version = response.Version;
            _codeSmells = response.LanguageCommon.CodeSmells;
            _languages = response.FileTypes;
        }
        public decimal GetVersion() => _version;

        public bool IsAnyCodeSmellSupported(IEnumerable<string> codeSmells) => codeSmells.Intersect(_codeSmells.ToList()).Any();

        public bool IsSupportedCodeSmell(string codeSmell) => _codeSmells.Contains(codeSmell);

        public bool IsSupportedLanguage(string extenison) => _languages.Contains(extenison.Replace(".", "").ToLower());

        public bool IsSupportedLanguageAndCodeSmell(string extenison, string codeSmell) => IsSupportedLanguage(extenison) && IsSupportedCodeSmell(codeSmell);
    }
}
