// Copyright (c) CodeScene. All rights reserved.

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
            _cache = new BaselineReviewCacheService();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cache.Clear();
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
            _cache.Put("other/file.cs", "content3", "score3");

            _cache.Invalidate("path/file.cs");

            var (found1, _) = _cache.Get("path/file.cs", "content1");
            var (found2, _) = _cache.Get("path/file.cs", "content2");
            var (found3, raw3) = _cache.Get("other/file.cs", "content3");
            Assert.IsFalse(found1);
            Assert.IsFalse(found2);
            Assert.IsTrue(found3);
            Assert.AreEqual("score3", raw3);
        }
    }
}
