// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public class CachingCodeReviewer : ICodeReviewer
    {
        private readonly ICodeReviewer _innerReviewer;
        private readonly ReviewCacheService _cache;
        private readonly BaselineReviewCacheService _baselineCache;
        private readonly DeltaCacheService _deltaCache;
        private readonly ILogger _logger;
        private readonly IGitService _git;
        private readonly ITelemetryManager _telemetryManager;
        private readonly ConcurrentDictionary<string, Task<FileReviewModel>> _pendingReviews = new ConcurrentDictionary<string, Task<FileReviewModel>>();

        public CachingCodeReviewer(
            ICodeReviewer innerReviewer,
            ReviewCacheService cache = null,
            BaselineReviewCacheService baselineCache = null,
            DeltaCacheService deltaCache = null,
            ILogger logger = null,
            IGitService git = null,
            ITelemetryManager telemetryManager = null)
        {
            _innerReviewer = innerReviewer;
            _cache = cache ?? new ReviewCacheService();
            _baselineCache = baselineCache ?? new BaselineReviewCacheService();
            _deltaCache = deltaCache ?? new DeltaCacheService();
            _logger = logger;
            _git = git;
            _telemetryManager = telemetryManager;
        }

        public async Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var normalizedPath = path.ToLowerInvariant();
            var query = new ReviewCacheQuery(content, normalizedPath);
            var cached = _cache.Get(query);

            if (cached != null)
            {
                _logger?.Debug($"CachingCodeReviewer: Cache hit for '{path}'.");
                return cached;
            }

            var pendingKey = GetPendingKey(content, path);

            var pendingTask = _pendingReviews.GetOrAdd(pendingKey, _ =>
                ReviewInternalAsync(path, content, isBaseline, cancellationToken));

            try
            {
                return await pendingTask;
            }
            finally
            {
                _pendingReviews.TryRemove(pendingKey, out _);
            }
        }

        public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, CancellationToken cancellationToken = default)
        {
            var normalizedPath = path.ToLowerInvariant();
            var reviewQuery = new ReviewCacheQuery(currentCode, normalizedPath);
            var cachedReview = _cache.Get(reviewQuery);

            FileReviewModel review;
            if (cachedReview != null)
            {
                _logger?.Debug($"CachingCodeReviewer: ReviewAndBaselineAsync - review cache hit for '{path}'.");
                review = cachedReview;
            }
            else
            {
                _logger?.Debug($"CachingCodeReviewer: ReviewAndBaselineAsync - review cache miss for '{path}', calling inner reviewer.");
                review = await _innerReviewer.ReviewAsync(path, currentCode, isBaseline: false, cancellationToken);

                if (review != null)
                {
                    var entry = new ReviewCacheEntry(currentCode, normalizedPath, review);
                    _cache.Put(entry);
                }
            }

            var baselineRawScore = await GetOrComputeBaselineRawScoreAsync(path, null, cancellationToken);

            return (review, baselineRawScore ?? string.Empty);
        }

        public async Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, CancellationToken cancellationToken = default)
        {
            var oldCode = GetBaselineContent(path, baselineContent);

            if (string.IsNullOrEmpty(oldCode))
            {
                return string.Empty;
            }

            var baselineEntry = _baselineCache.Get(path, oldCode);
            if (baselineEntry.Found)
            {
                _logger?.Debug($"CachingCodeReviewer: Baseline cache hit for '{path}'.");
                return baselineEntry.RawScore ?? string.Empty;
            }

            return await ComputeAndCacheBaselineAsync(path, oldCode, cancellationToken);
        }

        public async Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, CancellationToken cancellationToken = default)
        {
            var path = review.FilePath;

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger?.Warn("Could not review file, missing file path.");
                return null;
            }

            try
            {
                return await ComputeDeltaInternalAsync(review, currentCode, precomputedBaselineRawScore, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger?.Error($"Could not perform delta analysis on file {path}", e);
                return null;
            }
        }

        private string GetBaselineContent(string path, string baselineContent)
        {
            if (!string.IsNullOrEmpty(baselineContent))
            {
                return baselineContent;
            }

            if (_git != null)
            {
                return _git.GetFileContentForCommit(path) ?? string.Empty;
            }

            return string.Empty;
        }

        private async Task<string> ComputeAndCacheBaselineAsync(string path, string oldCode, CancellationToken cancellationToken)
        {
            _logger?.Debug($"CachingCodeReviewer: Baseline cache miss for '{path}', calling inner reviewer.");
            var oldCodeReview = await _innerReviewer.ReviewAsync(path, oldCode, isBaseline: true, cancellationToken);
            var oldRawScore = oldCodeReview?.RawScore ?? string.Empty;

            if (oldCodeReview?.RawScore != null)
            {
                _baselineCache.Put(path, oldCode, oldCodeReview.RawScore);
            }

            return oldRawScore;
        }

        private async Task<DeltaResponseModel> ComputeDeltaInternalAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore, CancellationToken cancellationToken)
        {
            var path = review.FilePath;
            var currentRawScore = review.RawScore ?? string.Empty;
            var oldCode = _git?.GetFileContentForCommit(path) ?? string.Empty;

            if (oldCode == currentCode)
            {
                _logger?.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: content unchanged.");
                _deltaCache.Put(new DeltaCacheEntry(path, oldCode, currentCode, null));
                return null;
            }

            var cacheQuery = new DeltaCacheQuery(path, oldCode, currentCode);
            var entry = _deltaCache.Get(cacheQuery);

            if (entry.Item1)
            {
                _logger?.Debug($"CachingCodeReviewer: Delta cache hit for '{path}'.");
                return entry.Item2;
            }

            var oldRawScore = precomputedBaselineRawScore
                ?? await GetOrComputeBaselineRawScoreAsync(path, oldCode, cancellationToken);

            if (oldRawScore == currentRawScore)
            {
                _logger?.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: scores identical.");
                _deltaCache.Put(new DeltaCacheEntry(path, oldCode, currentCode, null));
                return null;
            }

            var parameters = new DeltaComputationParameters(
                currentCode: currentCode,
                oldCode: oldCode,
                oldRawScore: oldRawScore);
            return await ComputeAndCacheDeltaAsync(review, parameters, cancellationToken);
        }

        private async Task<DeltaResponseModel> ComputeAndCacheDeltaAsync(FileReviewModel review, DeltaComputationParameters parameters, CancellationToken cancellationToken)
        {
            var path = review.FilePath;
            _logger?.Debug($"CachingCodeReviewer: Delta cache miss for '{path}', calling inner reviewer.");
            var delta = await _innerReviewer.DeltaAsync(review, parameters.CurrentCode, parameters.OldRawScore, cancellationToken);

            var cacheSnapshot = new Dictionary<string, DeltaResponseModel>(_deltaCache.GetAll());
            var cacheEntry = new DeltaCacheEntry(path, parameters.OldCode, parameters.CurrentCode, delta);
            _deltaCache.Put(cacheEntry);

            if (_telemetryManager != null)
            {
                DeltaTelemetryHelper.HandleDeltaTelemetryEvent(
                    cacheSnapshot, _deltaCache.GetAll(), cacheEntry, _telemetryManager);
            }

            return delta;
        }

        private async Task<FileReviewModel> ReviewInternalAsync(string path, string content, bool isBaseline, CancellationToken cancellationToken)
        {
            _logger?.Debug($"CachingCodeReviewer: Cache miss for '{path}', calling inner reviewer.");
            var result = await _innerReviewer.ReviewAsync(path, content, isBaseline, cancellationToken);

            if (result != null)
            {
                var entry = new ReviewCacheEntry(content, path.ToLowerInvariant(), result);
                _cache.Put(entry);
                _logger?.Debug($"CachingCodeReviewer: Cached result for '{path}'.");
            }

            return result;
        }

        private string GetPendingKey(string content, string path)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                var hashBytes = sha.ComputeHash(bytes);
                var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
                return path.ToLowerInvariant() + "|" + hash;
            }
        }

        private class DeltaComputationParameters
        {
            public DeltaComputationParameters(string currentCode, string oldCode, string oldRawScore)
            {
                CurrentCode = currentCode;
                OldCode = oldCode;
                OldRawScore = oldRawScore;
            }

            public string CurrentCode { get; }

            public string OldCode { get; }

            public string OldRawScore { get; }
        }
    }
}
