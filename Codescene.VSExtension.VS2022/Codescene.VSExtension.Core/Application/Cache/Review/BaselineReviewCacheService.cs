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
        private static readonly ConcurrentDictionary<string, (string RawScore, long RulesGeneration)> SharedCache = new ConcurrentDictionary<string, (string, long)>();
        private readonly ConcurrentDictionary<string, (string RawScore, long RulesGeneration)> _cache;

        public BaselineReviewCacheService()
        {
            _cache = SharedCache;
        }

        public BaselineReviewCacheService(ConcurrentDictionary<string, (string RawScore, long RulesGeneration)> store)
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
            if (!_cache.TryGetValue(key, out var entry) || entry.RulesGeneration != RulesGeneration.Current)
            {
                return (false, null);
            }

            return (true, entry.RawScore);
        }

        public void Put(string filePath, string baselineContent, string rawScore)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(baselineContent))
            {
                return;
            }

            var key = CacheKey(filePath, baselineContent);
            _cache[key] = (rawScore ?? string.Empty, RulesGeneration.Current);
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
                var sep = key.IndexOf('|');
                if (sep < 0)
                {
                    continue;
                }

                var pathFromKey = key.Substring(0, sep);
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
