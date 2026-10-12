// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.VS2022.Application.Cli
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CachingCodeReviewerProvider : ICodeReviewer
    {
        private readonly ICodeReviewer _inner;

        [ImportingConstructor]
        public CachingCodeReviewerProvider(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            ITelemetryManager telemetryManager,
            IGitService git,
            IPreflightManager preflightManager,
            IReviewPipeline pipeline,
            CodeHealthMonitorNotifier notifier,
            ISettingsProvider settings)
        {
            _inner = new CodeReviewer(logger, mapper, executor, telemetryManager, git, notifier, preflightManager, pipeline, settings);
        }

        public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewAsync(path, content, isBaseline, operationGeneration, cancellationToken);
        }

        public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            return _inner.DeltaAsync(review, currentCode, precomputedBaselineRawScore, operationGeneration, cancellationToken, baselineCommit);
        }

        public Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            return _inner.ReviewAndBaselineAsync(path, currentCode, operationGeneration, cancellationToken, baselineCommit);
        }

        public Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            return _inner.ReviewWithDeltaAsync(path, content, operationGeneration, cancellationToken, baselineCommit);
        }

        public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            return _inner.GetOrComputeBaselineRawScoreAsync(path, baselineContent, operationGeneration, cancellationToken, baselineCommit);
        }
    }
}
