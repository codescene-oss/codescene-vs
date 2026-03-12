// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public class ReviewCacheService : CacheService<ReviewCacheQuery, ReviewCacheEntry, ReviewCacheItem, FileReviewModel>
    {
        public ReviewCacheService()
            : base()
        {
        }

        public ReviewCacheService(ConcurrentDictionary<string, ReviewCacheItem> store, long testGenerationOverride = 0)
            : base(store, testGenerationOverride)
        {
        }

        public override FileReviewModel Get(ReviewCacheQuery query)
        {
            string cacheKey = GetCacheKey(query.FilePath, query.IsBaseline);
            string fileContents = query.FileContents;
            string contentHash = Hash(fileContents);

            if (Cache.TryGetValue(cacheKey, out var cachedItem))
            {
                bool cacheHit = cachedItem.FileContentsHash == contentHash && cachedItem.IsBaseline == query.IsBaseline && cachedItem.CacheGeneration == CacheGeneration.Current;
                return cacheHit ? cachedItem.Response : null;
            }

            return null;
        }

        public override void Put(ReviewCacheEntry entry, long? operationGeneration = null)
        {
            if (!IsStillCurrentGeneration(operationGeneration))
            {
                return;
            }

            string cacheKey = GetCacheKey(entry.FilePath, entry.IsBaseline);
            string fileContents = entry.FileContents;
            string contentHash = Hash(fileContents);

            var cacheItem = new ReviewCacheItem(contentHash, entry.Response, entry.IsBaseline, CacheGeneration.Current);
            Cache[cacheKey] = cacheItem;
        }

        public override void Invalidate(string key)
        {
            base.Invalidate(GetCacheKey(key, false));
            base.Invalidate(GetCacheKey(key, true));
        }

        public override void UpdateKey(string oldKey, string newKey)
        {
            if (Cache.TryGetValue(GetCacheKey(oldKey, false), out var entryFalse))
            {
                Cache[GetCacheKey(newKey, false)] = entryFalse;
                base.Invalidate(GetCacheKey(oldKey, false));
            }

            if (Cache.TryGetValue(GetCacheKey(oldKey, true), out var entryTrue))
            {
                Cache[GetCacheKey(newKey, true)] = entryTrue;
                base.Invalidate(GetCacheKey(oldKey, true));
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

        public bool CleanupOldGenerations()
        {
            var cacheGeneration = CacheGeneration.Current;
            var entriesToClean = GetEntriesToClean(cacheGeneration);

            if (entriesToClean.Any())
            {
                RemoveEntries(entriesToClean);
                return true;
            }

            return false;
        }

        private string GetRootPrefix(string gitRootPath)
        {
            return Path.GetFullPath(gitRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        private List<string> GetKeysToRemove(string rootPrefix)
        {
            var keysToRemove = new List<string>();
            var baselineSuffix = "|baseline=";

            foreach (var key in Cache.Keys)
            {
                var sep = key.IndexOf(baselineSuffix, StringComparison.Ordinal);
                if (sep < 0)
                {
                    continue;
                }

                var pathFromKey = key.Substring(0, sep);
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
                Cache.TryRemove(k, out _);
            }
        }

        private List<string> GetEntriesToClean(long cacheGeneration)
        {
            var entriesToClean = new List<string>();

            foreach (var pair in Cache)
            {
                if (pair.Value.CacheGeneration != cacheGeneration)
                {
                    entriesToClean.Add(pair.Key);
                }
            }

            return entriesToClean;
        }

        private void RemoveEntries(List<string> entriesToClean)
        {
            foreach (var entry in entriesToClean)
            {
                Cache.TryRemove(entry, out _);
            }
        }

        private string GetCacheKey(string filePath, bool isBaseline)
        {
            return filePath.ToLowerInvariant() + "|baseline=" + isBaseline;
        }
    }
}
