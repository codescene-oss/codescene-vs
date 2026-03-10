// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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

        public ReviewCacheService(ConcurrentDictionary<string, ReviewCacheItem> store)
            : base(store)
        {
        }

        public override FileReviewModel Get(ReviewCacheQuery query)
        {
            string cacheKey = GetCacheKey(query.FilePath, query.IsBaseline);
            string fileContents = query.FileContents;
            string contentHash = Hash(fileContents);

            if (Cache.TryGetValue(cacheKey, out var cachedItem))
            {
                bool cacheHit = cachedItem.FileContentsHash == contentHash && cachedItem.IsBaseline == query.IsBaseline && cachedItem.RulesGeneration == RulesGeneration.Current;
                return cacheHit ? cachedItem.Response : null;
            }

            return null;
        }

        public override void Put(ReviewCacheEntry entry)
        {
            string cacheKey = GetCacheKey(entry.FilePath, entry.IsBaseline);
            string fileContents = entry.FileContents;
            string contentHash = Hash(fileContents);

            var cacheItem = new ReviewCacheItem(contentHash, entry.Response, entry.IsBaseline, RulesGeneration.Current);
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

        public void RemoveEntriesOutsideRoot(string gitRootPath)
        {
            if (string.IsNullOrEmpty(gitRootPath))
            {
                return;
            }

            var rootPrefix = Path.GetFullPath(gitRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
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
                Cache.TryRemove(k, out _);
            }
        }

        private string GetCacheKey(string filePath, bool isBaseline)
        {
            return filePath.ToLowerInvariant() + "|baseline=" + isBaseline;
        }
    }
}
