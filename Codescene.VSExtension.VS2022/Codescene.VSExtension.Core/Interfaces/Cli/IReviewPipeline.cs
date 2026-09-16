// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IReviewPipeline
    {
        Task<(CliReviewModel Review, DeltaResponseModel Delta)> SubmitAsync(string repoRoot, ReviewSubmission submission);

        Task SubmitBatchAsync(string repoRoot, ReviewSubmission[] submissions);

        void Remove(string repoRoot, params ReviewDocument[] documents);

        void Invalidate();

        void Reset();

        void SetActiveRepos(IReadOnlyCollection<string> repoRoots);
    }
}
