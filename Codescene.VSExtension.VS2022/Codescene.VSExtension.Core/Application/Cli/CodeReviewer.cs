// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;

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

        public CodeReviewer(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            ITelemetryManager telemetryManager,
            IGitService git,
            ICodeHealthMonitorNotifier notifier = null)
        {
            _logger = logger;
            _mapper = mapper;
            _executor = executor;
            _telemetryManager = telemetryManager;
            _git = git;
            _notifier = notifier;
        }

        public async Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
            {
                _logger.Debug($"Skipping review for '{path}'. Missing content or file path.");
                return null;
            }

            var review = await _executor.ReviewContentAsync(fileName, content, isBaseline, cancellationToken);
            return _mapper.Map(path, review);
        }

        public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var oldCode = _git.GetFileContentForCommit(path) ?? string.Empty;
            var reviewTask = ReviewAsync(path, currentCode, false, operationGeneration, cancellationToken);
            var baselineTask = GetOrComputeBaselineRawScoreInternalAsync(path, oldCode, operationGeneration, cancellationToken);
            await Task.WhenAll(reviewTask, baselineTask).ConfigureAwait(false);
            var review = await reviewTask;
            var baselineRawScore = (await baselineTask) ?? string.Empty;
            return (review, baselineRawScore);
        }

        public async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var (review, baselineRawScore) = await ReviewAndBaselineAsync(path, content, operationGeneration, cancellationToken);
            if (review?.RawScore == null)
            {
                return (review, null);
            }

            var delta = await DeltaAsync(review, content, baselineRawScore, operationGeneration, cancellationToken);
            return (review, delta);
        }

        public async Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default)
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
                var oldCode = _git.GetFileContentForCommit(path);
                var oldRawScore = precomputedBaselineRawScore ?? await GetOrComputeBaselineRawScoreInternalAsync(path, oldCode, operationGeneration, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var delta = await _executor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = oldRawScore, NewScore = currentRawScore, FilePath = path, FileContent = currentCode }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                return delta;
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

        public async Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            return await GetOrComputeBaselineRawScoreInternalAsync(path, baselineContent, operationGeneration, cancellationToken);
        }

        private bool InvalidateCacheIfUnchanged(string path, string old, string current, DeltaCacheService cache)
        {
            if (old == current)
            {
                // Invalidate delta cache to remove file and from monitor if it was previously shown
                if (cache.Contains(path))
                {
                    cache.Invalidate(path);
                    _notifier?.RequestViewUpdate();
                }

                return true;
            }

            return false;
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
