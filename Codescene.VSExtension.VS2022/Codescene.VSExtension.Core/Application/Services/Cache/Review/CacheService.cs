using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review
{
    /// <summary>
    /// A generic base class for implementing a typed caching mechanism.
    /// </summary>
    /// <typeparam name="Q">The type of the query used to look up values in the cache.</typeparam>
    /// <typeparam name="E">The type of the entry object used to populate the cache.</typeparam>
    /// <typeparam name="V">The internal value type stored in the cache (usually a lightweight wrapper).</typeparam>
    /// <typeparam name="R">The type of the result returned by the cache lookup.</typeparam>
    public abstract class CacheService<Q, E, V, R>
    {
        protected static readonly ConcurrentDictionary<string, V> Cache = new ConcurrentDictionary<string, V>();

        protected string Hash(string content)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                var hashBytes = sha.ComputeHash(bytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public abstract R Get(Q query);
        public abstract void Put(E entry);

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

        public virtual void Clear()
        {
            Cache.Clear();
        }

        public IEnumerable<string> GetAllKeys()
        {
            return Cache.Keys;
        }

        public virtual void Remove(string filePath)
        {
            Cache.TryRemove(filePath, out _);
        }
    }
}
