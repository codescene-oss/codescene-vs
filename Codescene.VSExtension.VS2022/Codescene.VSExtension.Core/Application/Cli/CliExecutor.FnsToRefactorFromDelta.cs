// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public partial class CliExecutor
    {
        public Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResult, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
        {
            return ExecuteFnsToRefactorAsync(
                deltaResult != null,
                "Skipping refactoring functions from delta. Delta result was null.",
                cachePath => new FnsToRefactorDeltaRequestModel
                {
                    FileName = fileName,
                    FileContent = fileContent,
                    CachePath = cachePath,
                    Preflight = preflight,
                    DeltaResult = deltaResult,
                },
                "ACE refactoring functions from delta check",
                cancellationToken);
        }
    }
}
