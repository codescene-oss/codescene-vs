using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.PreflightManager
{
    public interface IPreflightManager
    {
        bool IsSupportedLanguage(string extenison);
        PreFlightResponseModel RunPreflight(bool force = false);
        PreFlightResponseModel GetPreflightResponse();
    }
}
