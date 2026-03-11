// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using System.IO;
using Codescene.VSExtension.Core.Application.Cache.Review;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class BaselineReviewCacheServiceTests
    {
        private BaselineReviewCacheService _cache;

        [TestInitialize]
        public void Setup()
        {
            _cache = new BaselineReviewCacheService(new ConcurrentDictionary<string, (string RawScore, long CacheGeneration)>(), testGenerationOverride: 0);
        }

        [TestMethod]
        public void Get_WithNullFilePath_ReturnsNotFound()
        {
            var (found, rawScore) = _cache.Get(null, "content");

            Assert.IsFalse(found);
            Assert.IsNull(rawScore);
        }

        [TestMethod]
        public void Get_WithEmptyFilePath_ReturnsNotFound()
        {
            var (found, rawScore) = _cache.Get(string.Empty, "content");

            Assert.IsFalse(found);
            Assert.IsNull(rawScore);
        }

        [TestMethod]
        public void Get_WithNullBaselineContent_ReturnsNotFound()
        {
            var (found, rawScore) = _cache.Get("file.cs", null);

            Assert.IsFalse(found);
            Assert.IsNull(rawScore);
        }

        [TestMethod]
        public void Get_WithEmptyBaselineContent_ReturnsNotFound()
        {
            var (found, rawScore) = _cache.Get("file.cs", string.Empty);

            Assert.IsFalse(found);
            Assert.IsNull(rawScore);
        }

        [TestMethod]
        public void Get_WhenCached_ReturnsScore()
        {
            _cache.Put("file.cs", "content", "raw-score");

            var (found, rawScore) = _cache.Get("file.cs", "content");

            Assert.IsTrue(found);
            Assert.AreEqual("raw-score", rawScore);
        }

        [TestMethod]
        public void Get_AfterCacheGenerationIncrement_ReturnsNotFound()
        {
            CacheGeneration.Reset();
            var testCache = new BaselineReviewCacheService(new ConcurrentDictionary<string, (string RawScore, long CacheGeneration)>());

            testCache.Put("file.cs", "content", "raw-score");
            Assert.IsTrue(testCache.Get("file.cs", "content").Found);

            CacheGeneration.Increment();

            try
            {
                var (found, rawScore) = testCache.Get("file.cs", "content");
                Assert.IsFalse(found);
                Assert.IsNull(rawScore);
            }
            finally
            {
                CacheGeneration.Reset();
            }
        }

        [TestMethod]
        public void Get_WhenNotCached_ReturnsNotFound()
        {
            var (found, rawScore) = _cache.Get("file.cs", "content");

            Assert.IsFalse(found);
            Assert.IsNull(rawScore);
        }

        [TestMethod]
        public void Put_WithValidInput_StoresInCache()
        {
            _cache.Put("file.cs", "content", "raw-score");

            var (found, rawScore) = _cache.Get("file.cs", "content");

            Assert.IsTrue(found);
            Assert.AreEqual("raw-score", rawScore);
        }

        [TestMethod]
        public void Put_WithNullFilePath_DoesNotStore()
        {
            _cache.Put(null, "content", "raw-score");

            var (found, _) = _cache.Get("file.cs", "content");

            Assert.IsFalse(found);
        }

        [TestMethod]
        public void Put_WithEmptyFilePath_DoesNotStore()
        {
            _cache.Put(string.Empty, "content", "raw-score");

            var (found, _) = _cache.Get("file.cs", "content");

            Assert.IsFalse(found);
        }

        [TestMethod]
        public void Put_WithNullBaselineContent_DoesNotStore()
        {
            _cache.Put("file.cs", null, "raw-score");

            var (found, _) = _cache.Get("file.cs", "content");

            Assert.IsFalse(found);
        }

        [TestMethod]
        public void Put_WithEmptyBaselineContent_DoesNotStore()
        {
            _cache.Put("file.cs", string.Empty, "raw-score");

            var (found, _) = _cache.Get("file.cs", "content");

            Assert.IsFalse(found);
        }

        [TestMethod]
        public void Put_WithNullRawScore_StoresEmptyString()
        {
            _cache.Put("file.cs", "content", null);

            var (found, rawScore) = _cache.Get("file.cs", "content");

            Assert.IsTrue(found);
            Assert.AreEqual(string.Empty, rawScore);
        }

        [TestMethod]
        public void Clear_RemovesAllEntries()
        {
            _cache.Put("file.cs", "content", "raw-score");

            _cache.Clear();

            var (found, _) = _cache.Get("file.cs", "content");
            Assert.IsFalse(found);
        }

        [TestMethod]
        public void Invalidate_WithNullFilePath_DoesNothing()
        {
            _cache.Put("file.cs", "content", "raw-score");

            _cache.Invalidate(null);

            var (found, _) = _cache.Get("file.cs", "content");
            Assert.IsTrue(found);
        }

        [TestMethod]
        public void Invalidate_WithEmptyFilePath_DoesNothing()
        {
            _cache.Put("file.cs", "content", "raw-score");

            _cache.Invalidate(string.Empty);

            var (found, _) = _cache.Get("file.cs", "content");
            Assert.IsTrue(found);
        }

        [TestMethod]
        public void Invalidate_WithMatchingFilePath_RemovesEntries()
        {
            _cache.Put("path/file.cs", "content1", "score1");
            _cache.Put("path/file.cs", "content2", "score2");
            _cache.Put("path/file.csx", "contentX", "scoreX");
            _cache.Put("other/file.cs", "content3", "score3");

            _cache.Invalidate("path/file.cs");

            var (found1, _) = _cache.Get("path/file.cs", "content1");
            var (found2, _) = _cache.Get("path/file.cs", "content2");
            var (foundX, rawX) = _cache.Get("path/file.csx", "contentX");
            var (found3, raw3) = _cache.Get("other/file.cs", "content3");
            Assert.IsFalse(found1);
            Assert.IsFalse(found2);
            Assert.IsTrue(foundX);
            Assert.AreEqual("scoreX", rawX);
            Assert.IsTrue(found3);
            Assert.AreEqual("score3", raw3);
        }

        [TestMethod]
        public void RemoveEntriesOutsideRoot_NullOrEmptyRoot_DoesNothing()
        {
            _cache.Put("file.cs", "content", "score");
            _cache.RemoveEntriesOutsideRoot(null);
            var (found, _) = _cache.Get("file.cs", "content");
            Assert.IsTrue(found);
            _cache.RemoveEntriesOutsideRoot(string.Empty);
            var (found2, _) = _cache.Get("file.cs", "content");
            Assert.IsTrue(found2);
        }

        [TestMethod]
        public void RemoveEntriesOutsideRoot_RemovesEntriesOutsideRoot()
        {
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "baseline-cache-root"));
            var insidePath = Path.Combine(root, "sub", "file.cs");
            var outsidePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "other", "file.cs"));
            _cache.Put(insidePath, "c1", "s1");
            _cache.Put(outsidePath, "c2", "s2");
            _cache.RemoveEntriesOutsideRoot(root);
            var (foundInside, _) = _cache.Get(insidePath, "c1");
            var (foundOutside, _) = _cache.Get(outsidePath, "c2");
            Assert.IsTrue(foundInside);
            Assert.IsFalse(foundOutside);
        }

        [TestMethod]
        public void RemoveEntriesOutsideRoot_WhenGetFullPathThrows_RemovesKey()
        {
            var store = new ConcurrentDictionary<string, (string RawScore, long CacheGeneration)>();
            var key = "\0|abc123";
            store[key] = ("score", 0);
            var service = new BaselineReviewCacheService(store);
            service.RemoveEntriesOutsideRoot(Path.GetTempPath());
            Assert.IsFalse(store.ContainsKey(key));
        }
    }
}
