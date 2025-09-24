using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Application.Services.PreflightManager
{
    public interface IPreflightManager
    {
        bool IsSupportedLanguage(string extenison);
        PreFlightResponseModel RunPreflight(bool force = false);
        PreFlightResponseModel GetPreflightResponse();
        AutoRefactorConfig GetAutoRefactorConfig();
	}
}
