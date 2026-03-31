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
            if (!_cache.TryGetValue(key, out var entry))
            {
                return (false, null);
            }

            return (true, entry);
        }

        public void Put(string filePath, string baselineContent, string rawScore, long? operationGeneration = null)
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

        public bool RemoveEntriesOutsideRoot(string gitRootPath)
        {
            if (string.IsNullOrEmpty(gitRootPath))
            {
                return false;
            }

            var rootPrefix = GetRootPrefix(gitRootPath);
            var keysToRemove = GetKeysToRemove(rootPrefix);

            if (keysToRemove.Any())
            {
                RemoveKeys(keysToRemove);
                return true;
            }

            return false;
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

        private string GetRootPrefix(string gitRootPath)
        {
            return Path.GetFullPath(gitRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        private List<string> GetKeysToRemove(string rootPrefix)
        {
            var keysToRemove = new List<string>();

            foreach (var key in _cache.Keys)
            {
                if (!TryParseCacheKey(key, out var pathFromKey, out _))
                {
                    continue;
                }

                if (ShouldRemoveKey(pathFromKey, rootPrefix))
                {
                    keysToRemove.Add(key);
                }
            }

            return keysToRemove;
        }

        private bool ShouldRemoveKey(string pathFromKey, string rootPrefix)
        {
            try
            {
                var fullPath = Path.GetFullPath(pathFromKey);
                return fullPath.Length > 0 && !fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        private void RemoveKeys(List<string> keysToRemove)
        {
            foreach (var k in keysToRemove)
            {
                _cache.TryRemove(k, out _);
            }
        }
    }
}
