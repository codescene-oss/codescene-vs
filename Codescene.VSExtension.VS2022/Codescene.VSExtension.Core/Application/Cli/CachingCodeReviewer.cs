// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public class CachingCodeReviewer : ICodeReviewer
    {
        private readonly ICodeReviewer _innerReviewer;
        private readonly ReviewCacheService _cache;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, Task<FileReviewModel>> _pendingReviews = new ConcurrentDictionary<string, Task<FileReviewModel>>();

        public CachingCodeReviewer(ICodeReviewer innerReviewer, ReviewCacheService cache = null, ILogger logger = null)
        {
            _innerReviewer = innerReviewer;
            _cache = cache ?? new ReviewCacheService();
            _logger = logger;
        }

        public async Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(path))
            {
                _logger?.Debug($"CachingCodeReviewer: Null or empty content/path for '{path}', delegating to inner reviewer.");
                return await _innerReviewer.ReviewAsync(path, content, isBaseline, cancellationToken);
            }

            var query = new ReviewCacheQuery(content, path);
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

        public async Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, CancellationToken cancellationToken = default)
        {
            return await _innerReviewer.DeltaAsync(review, currentCode, precomputedBaselineRawScore, cancellationToken);
        }

        public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, CancellationToken cancellationToken = default)
        {
            return await _innerReviewer.ReviewAndBaselineAsync(path, currentCode, cancellationToken);
        }

        public async Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, CancellationToken cancellationToken = default)
        {
            return await _innerReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineContent, cancellationToken);
        }

        private async Task<FileReviewModel> ReviewInternalAsync(string path, string content, bool isBaseline, CancellationToken cancellationToken)
        {
            _logger?.Debug($"CachingCodeReviewer: Cache miss for '{path}', calling inner reviewer.");
            var result = await _innerReviewer.ReviewAsync(path, content, isBaseline, cancellationToken);

            if (result != null)
            {
                var entry = new ReviewCacheEntry(content, path, result);
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
    }
}
