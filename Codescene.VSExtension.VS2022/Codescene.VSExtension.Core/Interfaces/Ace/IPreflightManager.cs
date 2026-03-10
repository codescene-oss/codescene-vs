// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Interfaces.Ace
{
    public interface IPreflightManager
    {
        bool IsSupportedLanguage(string extension);

        Task<PreFlightResponseModel> RunPreflightAsync(bool force = false, CancellationToken cancellationToken = default);

        Task<PreFlightResponseModel> GetPreflightResponseAsync(CancellationToken cancellationToken = default);

        AutoRefactorConfig GetAutoRefactorConfig();

        void SetHasAceToken(bool hasAceToken);
    }
}
