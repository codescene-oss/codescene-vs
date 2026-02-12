// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Interfaces.Ace
{
    public interface IPreflightManager
    {
        bool IsSupportedLanguage(string extension);

        PreFlightResponseModel RunPreflight(bool force = false);

        PreFlightResponseModel GetPreflightResponse();

        AutoRefactorConfig GetAutoRefactorConfig();

        void SetHasAceToken(bool hasAceToken);
    }
}
