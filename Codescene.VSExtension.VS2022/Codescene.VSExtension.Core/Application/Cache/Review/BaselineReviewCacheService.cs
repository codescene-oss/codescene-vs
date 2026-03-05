// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public class BaselineReviewCacheService
    {
        private static readonly ConcurrentDictionary<string, string> SharedCache = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> _cache;

        public BaselineReviewCacheService()
        {
            _cache = SharedCache;
        }

        public BaselineReviewCacheService(ConcurrentDictionary<string, string> store)
        {
            _cache = store;
        }

        public (bool Found, string RawScore) Get(string filePath, string baselineContent)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(baselineContent))
            {
                return (false, null);
            }

            var key = CacheKey(filePath, baselineContent);
            return _cache.TryGetValue(key, out var rawScore) ? (true, rawScore) : (false, null);
        }

        public void Put(string filePath, string baselineContent, string rawScore)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(baselineContent))
            {
                return;
            }

            var key = CacheKey(filePath, baselineContent);
            _cache[key] = rawScore ?? string.Empty;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void Invalidate(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var prefix = filePath.ToLowerInvariant() + "|";
            foreach (var key in _cache.Keys.Where(k => k.StartsWith(prefix)).ToList())
            {
                _cache.TryRemove(key, out _);
            }
        }

        private static string CacheKey(string filePath, string baselineContent)
        {
            var hash = Hash(baselineContent);
            return filePath.ToLowerInvariant() + "|" + hash;
        }

        private static string Hash(string content)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = sha.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
