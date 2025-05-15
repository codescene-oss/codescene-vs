using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.PreflightManager
{
    public interface IPreflightManager
    {
        bool IsSupportedLanguage(string extenison);
        bool IsSupportedCodeSmell(string codeSmell);
        bool IsAnyCodeSmellSupported(IEnumerable<string> codeSmells);
        bool IsSupportedLanguageAndCodeSmell(string extenison, string codeSmell);
        decimal GetVersion();
    }
}
