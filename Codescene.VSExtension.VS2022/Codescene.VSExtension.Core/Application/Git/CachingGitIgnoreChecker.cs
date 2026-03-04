// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class CachingGitIgnoreChecker
    {
        private readonly IGitIgnoreChecker _inner;
        private readonly object _cacheLock = new object();
        private Dictionary<string, bool> _cache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public CachingGitIgnoreChecker(IGitIgnoreChecker inner)
        {
            _inner = inner;
        }

        public int CacheCount
        {
            get
            {
                lock (_cacheLock)
                {
                    return _cache.Count;
                }
            }
        }

        public bool IsPathIgnored(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            if (PathUtilities.IsInGitDirectory(filePath))
            {
                return true;
            }

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(filePath, out var cached))
                {
                    return cached;
                }
            }

            var result = _inner.IsPathIgnored(filePath);

            lock (_cacheLock)
            {
                _cache[filePath] = result;
            }

            return result;
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
