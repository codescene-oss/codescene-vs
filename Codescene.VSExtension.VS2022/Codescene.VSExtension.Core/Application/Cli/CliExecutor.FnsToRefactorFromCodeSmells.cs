// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public partial class CliExecutor
    {
        public Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
        {
            return ExecuteFnsToRefactorAsync(
                codeSmells != null && codeSmells.Count > 0,
                "Skipping refactoring functions from code smells. Code smells list was null or empty.",
                cachePath => new FnsToRefactorCodeSmellRequestModel
                {
                    FileName = fileName,
                    FileContent = fileContent,
                    CachePath = cachePath,
                    Preflight = preflight,
                    CodeSmells = codeSmells,
                },
                "ACE refactoring functions from code smells check",
                cancellationToken);
        }
    }
}
