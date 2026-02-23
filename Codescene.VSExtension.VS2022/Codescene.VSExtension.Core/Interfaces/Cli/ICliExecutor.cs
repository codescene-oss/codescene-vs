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
        Task<DeltaResponseModel> ReviewDeltaAsync(ReviewDeltaRequest request, CancellationToken cancellationToken = default);

        Task<CliReviewModel> ReviewContentAsync(string filename, string content, bool isBaseLine = false, CancellationToken cancellationToken = default);

        Task<string> GetFileVersionAsync();

        Task<string> GetDeviceIdAsync();

        Task<PreFlightResponseModel> PreflightAsync(bool force = true);

        Task<RefactorResponseModel> PostRefactoringAsync(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null);

        Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight);

        Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResponse, PreFlightResponseModel preflight);
    }
}
