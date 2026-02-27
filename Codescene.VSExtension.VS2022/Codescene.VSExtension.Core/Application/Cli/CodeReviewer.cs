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

namespace Codescene.VSExtension.Core.Application.Cli
{
    public class CodeReviewer : ICodeReviewer
    {
        private readonly ILogger _logger;
        private readonly IModelMapper _mapper;
        private readonly ICliExecutor _executor;
        private readonly ITelemetryManager _telemetryManager;
        private readonly IGitService _git;

        public CodeReviewer(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            ITelemetryManager telemetryManager,
            IGitService git)
        {
            _logger = logger;
            _mapper = mapper;
            _executor = executor;
            _telemetryManager = telemetryManager;
            _git = git;
        }

        public async Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
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

        public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, CancellationToken cancellationToken = default)
        {
            var oldCode = _git.GetFileContentForCommit(path) ?? string.Empty;
            var reviewTask = ReviewAsync(path, currentCode, false, cancellationToken);
            var baselineTask = GetOrComputeBaselineRawScoreInternalAsync(path, oldCode, cancellationToken);
            await Task.WhenAll(reviewTask, baselineTask).ConfigureAwait(false);
            var review = await reviewTask;
            var baselineRawScore = (await baselineTask) ?? string.Empty;
            return (review, baselineRawScore);
        }

        public async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            var (review, baselineRawScore) = await ReviewAndBaselineAsync(path, content, cancellationToken);
            if (review?.RawScore == null)
            {
                return (review, null);
            }

            var delta = await DeltaAsync(review, content, baselineRawScore, cancellationToken);
            return (review, delta);
        }

        public async Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, CancellationToken cancellationToken = default)
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
                var cache = new DeltaCacheService();

                // Skip delta if content is identical (no changes since baseline)
                if (oldCode == currentCode)
                {
                    _logger.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: content unchanged since baseline.");
                    // Cache null delta to remove file from monitor if it was previously shown
                    cache.Put(new DeltaCacheEntry(path, oldCode, currentCode, null));
                    return null;
                }

                var entry = cache.Get(new DeltaCacheQuery(path, oldCode, currentCode));

                // If cache hit
                if (entry.Item1)
                {
                    return entry.Item2;
                }

                var oldRawScore = precomputedBaselineRawScore ?? await GetOrComputeBaselineRawScoreInternalAsync(path, oldCode, cancellationToken);

                // Skip delta if scores are identical (same code health, no meaningful change)
                if (oldRawScore == currentRawScore)
                {
                    _logger.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: scores are identical.");
                    // Cache null delta to remove file from monitor if it was previously shown
                    cache.Put(new DeltaCacheEntry(path, oldCode, currentCode, null));
                    return null;
                }

                var delta = await _executor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = oldRawScore, NewScore = currentRawScore, FilePath = path, FileContent = currentCode }, cancellationToken);

                var cacheSnapshot = new Dictionary<string, DeltaResponseModel>(cache.GetAll());
                var cacheEntry = new DeltaCacheEntry(path, oldCode, currentCode, delta);
                cache.Put(cacheEntry);

                DeltaTelemetryHelper.HandleDeltaTelemetryEvent(cacheSnapshot, cache.GetAll(), cacheEntry, _telemetryManager);

                return delta;
            }
            catch (Exception e)
            {
                _logger.Error($"Could not perform delta analysis on file {path}", e);
                return null;
            }
        }

        public async Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, CancellationToken cancellationToken = default)
        {
            return await GetOrComputeBaselineRawScoreInternalAsync(path, baselineContent, cancellationToken);
        }

        private async Task<string> GetOrComputeBaselineRawScoreInternalAsync(string path, string oldCode, CancellationToken cancellationToken)
        {
            var baselineCache = new BaselineReviewCacheService();
            var baselineEntry = baselineCache.Get(path, oldCode);
            if (baselineEntry.Found)
            {
                return baselineEntry.RawScore ?? string.Empty;
            }

            var oldCodeReview = await ReviewAsync(path, oldCode, isBaseline: true, cancellationToken);
            var oldRawScore = oldCodeReview?.RawScore ?? string.Empty;
            if (oldCodeReview?.RawScore != null)
            {
                baselineCache.Put(path, oldCode, oldCodeReview.RawScore);
            }

            return oldRawScore;
        }
    }
}
