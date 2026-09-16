// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public class CodeReviewer : ICodeReviewer
    {
        private readonly ILogger _logger;
        private readonly IModelMapper _mapper;
        private readonly ICliExecutor _executor;
        private readonly ITelemetryManager _telemetryManager;
        private readonly IGitService _git;
        private readonly ICodeHealthMonitorNotifier _notifier;
        private readonly IPreflightManager _preflightManager;
        private readonly IReviewPipeline _pipeline;

        public CodeReviewer(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            ITelemetryManager telemetryManager,
            IGitService git,
            ICodeHealthMonitorNotifier notifier = null,
            IPreflightManager preflightManager = null,
            IReviewPipeline pipeline = null)
        {
            _logger = logger;
            _mapper = mapper;
            _executor = executor;
            _telemetryManager = telemetryManager;
            _git = git;
            _notifier = notifier;
            _preflightManager = preflightManager;
            _pipeline = pipeline;
        }

        public async Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
            {
                _logger.Debug($"Skipping review for '{path}'. Missing content or file path.");
                return null;
            }

            _logger?.Info($"Reviewing file {path}...", true);

            var review = await _executor.ReviewContentAsync(path, content, isBaseline, cancellationToken);
            return _mapper.Map(path, review);
        }

        public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            var oldCode = _git.GetFileContentForCommit(path, baselineCommit) ?? string.Empty;
            var reviewTask = ReviewAsync(path, currentCode, false, operationGeneration, cancellationToken);
            var baselineTask = GetOrComputeBaselineRawScoreInternalAsync(path, oldCode, operationGeneration, cancellationToken);
            await Task.WhenAll(reviewTask, baselineTask).ConfigureAwait(false);
            var review = await reviewTask;
            var baselineRawScore = (await baselineTask) ?? string.Empty;
            return (review, baselineRawScore);
        }

        public async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            if (_pipeline != null)
            {
                return await ReviewWithPipelineAsync(path, content, cancellationToken);
            }

            var (review, baselineRawScore) = await ReviewAndBaselineAsync(path, content, operationGeneration, cancellationToken, baselineCommit);
            if (review?.RawScore == null)
            {
                return (review, null);
            }

            var delta = await DeltaAsync(review, content, baselineRawScore, operationGeneration, cancellationToken, baselineCommit);
            return (review, delta);
        }

        public async Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            var path = review.FilePath;
            var currentRawScore = review.RawScore ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.Warn("Could not review file, missing file path.");
                return null;
            }

            try
            {
                var oldCode = _git.GetFileContentForCommit(path, baselineCommit);
                var oldRawScore = precomputedBaselineRawScore ?? await GetOrComputeBaselineRawScoreInternalAsync(path, oldCode, operationGeneration, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var delta = await _executor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = oldRawScore, NewScore = currentRawScore, FilePath = path, FileContent = currentCode }, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return await EnrichDeltaAsync(path, currentCode, delta, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Error($"Could not perform delta analysis on file {path}", e);
                return null;
            }
        }

        public async Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default, string baselineCommit = null)
        {
            var oldCode = !string.IsNullOrEmpty(baselineContent)
                ? baselineContent
                : (_git?.GetFileContentForCommit(path, baselineCommit) ?? string.Empty);

            if (string.IsNullOrEmpty(oldCode))
            {
                return string.Empty;
            }

            return await GetOrComputeBaselineRawScoreInternalAsync(path, oldCode, operationGeneration, cancellationToken);
        }

        private async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithPipelineAsync(string path, string content, CancellationToken cancellationToken)
        {
            var repoRoot = GitPathDiscovery.TryGetWorkingDirectory(path);
            if (string.IsNullOrEmpty(repoRoot))
            {
                _logger.Debug($"Skipping pipeline review for '{path}'. Repository root was not found.");
                return (null, null);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var document = new ReviewDocument { FilePath = path, Content = content, IsDirty = true };
            var (cliReview, delta) = await _pipeline.SubmitAsync(
                repoRoot,
                new ReviewSubmission
                {
                    Document = document,
                    RelPath = RpcPath.RelativePosix(repoRoot, path),
                    Content = content,
                    UpdateDiagnosticsPane = true,
                    UpdateMonitor = true,
                });

            if (cliReview == null)
            {
                return (null, null);
            }

            var review = _mapper.Map(path, cliReview);
            if (review?.RawScore == null)
            {
                return (review, null);
            }

            var enriched = await EnrichDeltaAsync(path, content, delta, cancellationToken);
            return (review, enriched);
        }

        private async Task<DeltaResponseModel> EnrichDeltaAsync(string path, string currentCode, DeltaResponseModel delta, CancellationToken cancellationToken)
        {
            if (_preflightManager == null || delta == null)
            {
                return delta;
            }

            var preflight = await _preflightManager.GetPreflightResponseAsync(cancellationToken);
            var refactorableFunctions = await _executor.FnsToRefactorFromDeltaAsync(path, currentCode, delta, preflight, cancellationToken);
            if (refactorableFunctions is not { Count: > 0 })
            {
                return delta;
            }

            foreach (var refactorableFunction in refactorableFunctions)
            {
                var function = delta.FunctionLevelFindings?.FirstOrDefault(x => x.Function.Name == refactorableFunction.Name);
                if (function != null)
                {
                    function.RefactorableFn = refactorableFunction;
                }
            }

            return delta;
        }

        private async Task<string> GetOrComputeBaselineRawScoreInternalAsync(string path, string oldCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var baselineCache = new BaselineReviewCacheService();
            var baselineEntry = baselineCache.Get(path, oldCode);
            if (baselineEntry.Found)
            {
                return baselineEntry.RawScore ?? string.Empty;
            }

            var oldCodeReview = await ReviewAsync(path, oldCode, isBaseline: true, operationGeneration, cancellationToken);
            var oldRawScore = oldCodeReview?.RawScore ?? string.Empty;
            if (oldCodeReview?.RawScore != null)
            {
                baselineCache.Put(path, oldCode, oldCodeReview.RawScore, operationGeneration);
            }

            return oldRawScore;
        }
    }
}
