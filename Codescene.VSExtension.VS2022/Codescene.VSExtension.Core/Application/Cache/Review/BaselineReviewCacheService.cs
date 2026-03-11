// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public class BaselineReviewCacheService
    {
        private static readonly ConcurrentDictionary<string, (string RawScore, long CacheGeneration)> SharedCache = new ConcurrentDictionary<string, (string, long)>();
        private readonly ConcurrentDictionary<string, (string RawScore, long CacheGeneration)> _cache;
        private readonly long _capturedGeneration;

        public BaselineReviewCacheService()
        {
            _cache = SharedCache;
            _capturedGeneration = CacheGeneration.Current;
        }

        public BaselineReviewCacheService(ConcurrentDictionary<string, (string RawScore, long CacheGeneration)> store)
        {
            _cache = store;
            _capturedGeneration = CacheGeneration.Current;
        }

        public (bool Found, string RawScore) Get(string filePath, string baselineContent)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(baselineContent))
            {
                return (false, null);
            }

            var key = CacheKey(filePath, baselineContent);
            if (!_cache.TryGetValue(key, out var entry) || entry.CacheGeneration != CacheGeneration.Current)
            {
                return (false, null);
            }

            return (true, entry.RawScore);
        }

        public void Put(string filePath, string baselineContent, string rawScore)
        {
            if (CacheGeneration.Current != _capturedGeneration)
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(baselineContent))
            {
                return;
            }

            var key = CacheKey(filePath, baselineContent);
            _cache[key] = (rawScore ?? string.Empty, CacheGeneration.Current);
        }

        public void Clear()
        {
            CacheGeneration.Increment();
            _cache.Clear();
        }

        public void Invalidate(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var pathToMatch = filePath.ToLowerInvariant();
            foreach (var key in _cache.Keys.ToList())
            {
                if (!TryParseCacheKey(key, out var pathFromKey, out _))
                {
                    continue;
                }

                if (pathFromKey.Equals(pathToMatch, StringComparison.Ordinal))
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        public void RemoveEntriesOutsideRoot(string gitRootPath)
        {
            if (string.IsNullOrEmpty(gitRootPath))
            {
                return;
            }

            var rootPrefix = Path.GetFullPath(gitRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var keysToRemove = new List<string>();

            foreach (var key in _cache.Keys)
            {
                if (!TryParseCacheKey(key, out var pathFromKey, out _))
                {
                    continue;
                }

                try
                {
                    var fullPath = Path.GetFullPath(pathFromKey);
                    if (fullPath.Length > 0 && !fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(key);
                    }
                }
                catch
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var k in keysToRemove)
            {
                _cache.TryRemove(k, out _);
            }
        }

        private static bool TryParseCacheKey(string key, out string path, out string hash)
        {
            path = null;
            hash = null;
            var sep = key.IndexOf('|');
            if (sep < 0)
            {
                return false;
            }

            path = key.Substring(0, sep);
            hash = key.Substring(sep + 1);
            return true;
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
