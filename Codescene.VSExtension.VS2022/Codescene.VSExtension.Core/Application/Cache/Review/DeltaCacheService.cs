// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public class DeltaCacheService : CacheService<DeltaCacheQuery, DeltaCacheEntry, DeltaCacheItem, (bool, DeltaResponseModel)>
    {
        public DeltaCacheService()
            : base()
        {
        }

        public DeltaCacheService(ConcurrentDictionary<string, DeltaCacheItem> store, long testGenerationOverride = 0)
            : base(store, testGenerationOverride)
        {
        }

        /// <summary>
        /// Retrieves a cached delta response for the given query.
        ///
        /// This method distinguishes between:
        /// - A cache <b>hit</b>, where the file path and content hashes match
        ///   a stored entry. It returns the cached <see cref="DeltaResponseModel"/>,
        ///   which may be <c>null</c> if the API originally returned <c>null</c>.
        /// - A cache <b>miss</b> or <b>stale</b> entry, where no match is found
        ///   or content has changed. This is indicated by <c>false</c> in the tuple.
        /// </summary>
        public override (bool, DeltaResponseModel) Get(DeltaCacheQuery query)
        {
            var oldHash = Hash(query.BaselineContent);
            var newHash = Hash(query.CurrentContent);

            var cacheKey = GetCacheKey(query.FilePath);

            if (!Cache.TryGetValue(cacheKey, out var entry))
            {
                return (false, null);
            }

            var contentsMatch = entry.HeadHash == oldHash && entry.CurrentHash == newHash;
            var generationMatch = entry.CacheGeneration == CacheGeneration.Current;
            var isCacheHitOrNotStale = contentsMatch && generationMatch;

            return (isCacheHitOrNotStale, entry.Delta);
        }

        /// <summary>
        /// Adds or updates the delta cache for the given entry.
        ///
        /// The key is the file path, and the cache stores hashes of both the
        /// head and current content. These hashes are later used to check staleness.
        /// </summary>
        public override void Put(DeltaCacheEntry entry)
        {
            if (!IsCurrentGeneration())
            {
                return;
            }

            if (!File.Exists(entry.FilePath))
            {
                return;
            }

            var headHash = Hash(entry.BaselineContent);
            var currentContentHash = Hash(entry.CurrentFileContent);
            var cacheKey = GetCacheKey(entry.FilePath);
            Cache[cacheKey] = new DeltaCacheItem(entry.FilePath, headHash, currentContentHash, entry.Delta, CacheGeneration.Current);
        }

        public Dictionary<string, DeltaResponseModel> GetAll()
        {
            var result = new Dictionary<string, DeltaResponseModel>();

            foreach (var pair in Cache)
            {
                if (pair.Value.Delta != null)
                {
                    result[pair.Value.FilePath] = pair.Value.Delta;
                }
            }

            return result;
        }

        public DeltaResponseModel GetDeltaForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            var cacheKey = GetCacheKey(filePath);

            if (Cache.TryGetValue(cacheKey, out var item))
            {
                return item.Delta;
            }

            return null;
        }

        public override void Invalidate(string key)
        {
            base.Invalidate(GetCacheKey(key));
        }

        public override void UpdateKey(string oldKey, string newKey)
        {
            base.UpdateKey(GetCacheKey(oldKey), GetCacheKey(newKey));
        }

        public override bool Contains(string key)
        {
            return base.Contains(GetCacheKey(key));
        }

        public void RemoveEntriesOutsideRoot(string gitRootPath)
        {
            if (string.IsNullOrEmpty(gitRootPath))
            {
                return;
            }

            var rootPrefix = Path.GetFullPath(gitRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var keysToRemove = new List<string>();

            foreach (var pair in Cache)
            {
                var fullPath = Path.GetFullPath(pair.Value.FilePath ?? string.Empty);
                if (fullPath.Length > 0 && !fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                Cache.TryRemove(key, out _);
            }
        }

        private string GetCacheKey(string filePath)
        {
            return filePath.ToLowerInvariant();
        }
    }
}
