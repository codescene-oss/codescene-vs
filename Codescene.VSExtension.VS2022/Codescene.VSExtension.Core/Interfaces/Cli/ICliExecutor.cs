// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliExecutor
    {
        Task<string> GetFileVersionAsync(CancellationToken cancellationToken = default);

        Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default);

        Task<PreFlightResponseModel> PreflightAsync(bool force = true, CancellationToken cancellationToken = default);

        Task<RefactorResponseModel> PostRefactoringAsync(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null, CancellationToken cancellationToken = default);

        Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight, CancellationToken cancellationToken = default);

        Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResponse, PreFlightResponseModel preflight, CancellationToken cancellationToken = default);
    }
}
