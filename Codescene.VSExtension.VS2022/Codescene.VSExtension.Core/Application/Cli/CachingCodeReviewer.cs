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
        private readonly ICodeHealthMonitorNotifier _notifier;
        private readonly ConcurrentDictionary<string, Lazy<Task<FileReviewModel>>> _pendingReviews = new ConcurrentDictionary<string, Lazy<Task<FileReviewModel>>>();
        private readonly ConcurrentDictionary<string, Lazy<Task<DeltaResponseModel>>> _pendingDeltas = new ConcurrentDictionary<string, Lazy<Task<DeltaResponseModel>>>();

        public CachingCodeReviewer(
            ICodeReviewer innerReviewer,
            ReviewCacheService cache = null,
            BaselineReviewCacheService baselineCache = null,
            DeltaCacheService deltaCache = null,
            ILogger logger = null,
            IGitService git = null,
            ITelemetryManager telemetryManager = null,
            ICodeHealthMonitorNotifier notifier = null)
        {
            _innerReviewer = innerReviewer;
            _cache = cache ?? new ReviewCacheService();
            _baselineCache = baselineCache ?? new BaselineReviewCacheService();
            _deltaCache = deltaCache ?? new DeltaCacheService();
            _logger = logger;
            _git = git;
            _telemetryManager = telemetryManager;
            _notifier = notifier;
        }

        public async Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var normalizedPath = path.ToLowerInvariant();
            var query = new ReviewCacheQuery(content, normalizedPath, isBaseline);
            var cached = _cache.Get(query);

            if (cached != null)
            {
                _logger?.Debug($"CachingCodeReviewer: Cache hit for '{path}'.");
                return cached;
            }

            var pendingKey = GetPendingKey(content, path, isBaseline);

            var lazyTask = _pendingReviews.GetOrAdd(pendingKey, _ =>
                new Lazy<Task<FileReviewModel>>(() =>
                    ReviewInternalAsync(path, content, isBaseline, operationGeneration, CancellationToken.None)));

            var pendingTask = lazyTask.Value;

            try
            {
                var result = await pendingTask.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                _pendingReviews.TryRemove(pendingKey, out _);
            }
        }

        public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var review = await this.ReviewAsync(path, currentCode, isBaseline: false, operationGeneration, cancellationToken);
            var baselineRawScore = await GetOrComputeBaselineRawScoreAsync(path, null, operationGeneration, cancellationToken);

            return (review, baselineRawScore ?? string.Empty);
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

        public async Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default)
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

            return await ComputeAndCacheBaselineAsync(path, oldCode, operationGeneration, cancellationToken);
        }

        public async Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var path = review.FilePath;

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger?.Warn("Could not review file, missing file path.");
                return null;
            }

            var oldCode = _git?.GetFileContentForCommit(path) ?? string.Empty;

            if (oldCode == currentCode)
            {
                _logger?.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: content unchanged.");

                // If unchanged file exists in the cache, which could happen if user undos changes, clear and update views.
                if (_deltaCache.Contains(path))
                {
                    _deltaCache.Invalidate(path);
                    _notifier?.RequestViewUpdate();
                }

                return null;
            }

            var cacheQuery = new DeltaCacheQuery(path, oldCode, currentCode);
            var cacheEntry = _deltaCache.Get(cacheQuery);
            if (cacheEntry.Item1)
            {
                _logger?.Debug($"CachingCodeReviewer: Delta cache hit for '{path}'.");
                return cacheEntry.Item2;
            }

            var pendingKey = GetPendingDeltaKey(path, oldCode, currentCode, precomputedBaselineRawScore);
            var lazyTask = _pendingDeltas.GetOrAdd(pendingKey, _ =>
                new Lazy<Task<DeltaResponseModel>>(() =>
                    ComputeDeltaWithLifecycleAsync(review, currentCode, oldCode, precomputedBaselineRawScore, operationGeneration, CancellationToken.None)));
            var pendingTask = lazyTask.Value;
            try
            {
                var delta = await pendingTask.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return delta;
            }
            finally
            {
                _pendingDeltas.TryRemove(pendingKey, out _);
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

        private async Task<string> ComputeAndCacheBaselineAsync(string path, string oldCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            _logger?.Debug($"CachingCodeReviewer: Baseline cache miss for '{path}', calling reviewer.");
            var oldCodeReview = await this.ReviewAsync(path, oldCode, isBaseline: true, operationGeneration, cancellationToken);
            var oldRawScore = oldCodeReview?.RawScore ?? string.Empty;

            if (oldCodeReview?.RawScore != null)
            {
                _baselineCache.Put(path, oldCode, oldCodeReview.RawScore, operationGeneration);
            }

            return oldRawScore;
        }

        private async Task<DeltaResponseModel> ComputeDeltaInternalAsync(FileReviewModel review, DeltaComputationInput input, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var path = review.FilePath;
            var currentRawScore = review.RawScore ?? string.Empty;

            var oldRawScore = input.PrecomputedBaselineRawScore
                ?? await GetOrComputeBaselineRawScoreAsync(path, input.OldCode, operationGeneration, cancellationToken);

            if (oldRawScore == currentRawScore)
            {
                _logger?.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: scores identical.");
                if (_deltaCache.Contains(path))
                {
                    _deltaCache.Invalidate(path);
                    _notifier?.RequestViewUpdate();
                }

                return null;
            }

            var parameters = new DeltaComputationParameters(
                currentCode: input.CurrentCode,
                oldCode: input.OldCode,
                oldRawScore: oldRawScore);
            return await ComputeAndCacheDeltaAsync(review, parameters, operationGeneration, cancellationToken);
        }

        private async Task<DeltaResponseModel> ComputeAndCacheDeltaAsync(FileReviewModel review, DeltaComputationParameters parameters, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            var path = review.FilePath;
            _logger?.Debug($"CachingCodeReviewer: Delta cache miss for '{path}', calling inner reviewer.");
            var delta = await _innerReviewer.DeltaAsync(review, parameters.CurrentCode, parameters.OldRawScore, operationGeneration, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var cacheSnapshot = new Dictionary<string, DeltaResponseModel>(_deltaCache.GetAll());
            var cacheEntry = new DeltaCacheEntry(path, parameters.OldCode, parameters.CurrentCode, delta);
            _deltaCache.Put(cacheEntry, operationGeneration);

            if (_telemetryManager != null)
            {
                DeltaTelemetryHelper.HandleDeltaTelemetryEvent(
                    cacheSnapshot, _deltaCache.GetAll(), cacheEntry, _telemetryManager);
            }

            return delta;
        }

        private async Task<DeltaResponseModel> ComputeDeltaWithLifecycleAsync(
            FileReviewModel review,
            string currentCode,
            string oldCode,
            string precomputedBaselineRawScore,
            long? operationGeneration = null,
            CancellationToken cancellationToken = default)
        {
            var path = review.FilePath;
            _notifier?.OnDeltaStarting(path);
            try
            {
                try
                {
                    var computationParams = new DeltaComputationInput(currentCode, oldCode, precomputedBaselineRawScore);
                    return await ComputeDeltaInternalAsync(review, computationParams, operationGeneration, cancellationToken);
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
            finally
            {
                _notifier?.OnDeltaCompleted(path);
            }
        }

        private async Task<FileReviewModel> ReviewInternalAsync(string path, string content, bool isBaseline, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            _logger?.Debug($"CachingCodeReviewer: Cache miss for '{path}', calling inner reviewer.");
            var result = await _innerReviewer.ReviewAsync(path, content, isBaseline, operationGeneration, cancellationToken);
            if (result != null)
            {
                var entry = new ReviewCacheEntry(content, path.ToLowerInvariant(), result, isBaseline);
                _cache.Put(entry, operationGeneration);
                _logger?.Debug($"CachingCodeReviewer: Cached result for '{path}'.");
            }

            return result;
        }

        private string GetPendingKey(string content, string path, bool isBaseline)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                var hashBytes = sha.ComputeHash(bytes);
                var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
                return path.ToLowerInvariant() + "|" + hash + "|baseline=" + isBaseline;
            }
        }

        private string GetPendingDeltaKey(string path, string oldCode, string currentCode, string precomputedBaselineRawScore)
        {
            using (var sha = SHA256.Create())
            {
                var oldHash = ComputeHash(sha, oldCode ?? string.Empty);
                var currentHash = ComputeHash(sha, currentCode ?? string.Empty);
                var baselineHash = ComputeHash(sha, precomputedBaselineRawScore ?? string.Empty);
                return path.ToLowerInvariant() + "|delta|" + oldHash + "|" + currentHash + "|" + baselineHash;
            }
        }

        private string ComputeHash(HashAlgorithm algorithm, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = algorithm.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private class DeltaComputationInput
        {
            public DeltaComputationInput(string currentCode, string oldCode, string precomputedBaselineRawScore)
            {
                CurrentCode = currentCode;
                OldCode = oldCode;
                PrecomputedBaselineRawScore = precomputedBaselineRawScore;
            }

            public string CurrentCode { get; }

            public string OldCode { get; }

            public string PrecomputedBaselineRawScore { get; }
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
