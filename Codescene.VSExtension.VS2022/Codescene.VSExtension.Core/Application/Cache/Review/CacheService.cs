// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    /// <summary>
    /// A generic base class for implementing a typed caching mechanism.
    /// </summary>
    /// <typeparam name="TQ">The type of the query used to look up values in the cache.</typeparam>
    /// <typeparam name="TE">The type of the entry object used to populate the cache.</typeparam>
    /// <typeparam name="TV">The internal value type stored in the cache (usually a lightweight wrapper).</typeparam>
    /// <typeparam name="TR">The type of the result returned by the cache lookup.</typeparam>
    public abstract class CacheService<TQ, TE, TV, TR>
    {
        private static readonly ConcurrentDictionary<string, TV> SharedCache = new ConcurrentDictionary<string, TV>();
        private readonly ConcurrentDictionary<string, TV> _cache;
        private readonly long _capturedGeneration;
        private readonly long? _generationOverride;

        protected CacheService()
        {
            _cache = SharedCache;
            _capturedGeneration = CacheGeneration.Current;
        }

        protected CacheService(ConcurrentDictionary<string, TV> store, long? testGenerationOverride = null)
        {
            _cache = store ?? throw new ArgumentNullException(nameof(store));
            _generationOverride = testGenerationOverride;
            _capturedGeneration = testGenerationOverride ?? CacheGeneration.Current;
        }

        protected ConcurrentDictionary<string, TV> Cache => _cache;

        public abstract TR Get(TQ query);

        public abstract void Put(TE entry);

        public virtual void Invalidate(string key)
        {
            Cache.TryRemove(key, out _);
        }

        public virtual void UpdateKey(string oldKey, string newKey)
        {
            if (Cache.TryGetValue(oldKey, out var entry))
            {
                Cache[newKey] = entry;
                Invalidate(oldKey);
            }
        }

        public virtual bool Contains(string key)
        {
            return Cache.ContainsKey(key);
        }

        public virtual void Clear()
        {
            CacheGeneration.Increment();
            Cache.Clear();
        }

        protected bool IsCurrentGeneration()
        {
            if (_generationOverride.HasValue)
            {
                return true;
            }

            return CacheGeneration.Current == _capturedGeneration;
        }

        protected string Hash(string content)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = sha.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
