// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class DeltaCacheServiceTests
    {
        private DeltaCacheService _cacheService;

        [TestInitialize]
        public void Setup()
        {
            _cacheService = new DeltaCacheService();
            _cacheService.Clear(); // Ensure clean state for each test
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        private const string DefaultFilePath = "test.cs";
        private const string DefaultBaseline = "baseline code";
        private const string DefaultCurrent = "current code";

        [TestMethod]
        public void Get_EmptyCache_ReturnsFalseAndNull()
        {
            var result = _cacheService.Get(new DeltaCacheQuery("test.cs", "baseline content", "current content"));

            AssertCacheMiss(result);
        }

        [TestMethod]
        public void Get_CacheHitWithMatchingHashes_ReturnsTrueAndDelta()
        {
            var delta = CreateDelta(-0.5m);
            PutCacheEntry(DefaultFilePath, DefaultBaseline, DefaultCurrent, delta);

            var result = _cacheService.Get(new DeltaCacheQuery(DefaultFilePath, DefaultBaseline, DefaultCurrent));

            AssertCacheHit(result, -0.5m);
        }

        [TestMethod]
        public void Get_CacheHitWithNullDelta_ReturnsTrueAndNull()
        {
            PutCacheEntry(DefaultFilePath, DefaultBaseline, DefaultCurrent, delta: null);

            var result = _cacheService.Get(new DeltaCacheQuery(DefaultFilePath, DefaultBaseline, DefaultCurrent));

            Assert.IsTrue(result.Item1); // Cache hit
            Assert.IsNull(result.Item2); // But delta was null
        }

        [TestMethod]
        public void Get_DifferentBaselineContent_ReturnsStaleEntry()
        {
            var uniquePath = "baseline_test_" + System.Guid.NewGuid() + ".cs";
            PutCacheEntry(uniquePath, "original baseline", DefaultCurrent, CreateDelta(-1.0m));

            var result = _cacheService.Get(new DeltaCacheQuery(uniquePath, "different baseline", DefaultCurrent));

            // Cache returns stale entry (found=false) but still provides old delta for reference
            AssertStaleEntry(result, expectedScoreChange: -1.0m);
        }

        [TestMethod]
        public void Get_DifferentCurrentContent_ReturnsStaleEntry()
        {
            var uniquePath = "current_test_" + System.Guid.NewGuid() + ".cs";
            PutCacheEntry(uniquePath, DefaultBaseline, "original current", CreateDelta(-1.0m));

            var result = _cacheService.Get(new DeltaCacheQuery(uniquePath, DefaultBaseline, "different current"));

            // Cache returns stale entry (found=false) but still provides old delta for reference
            AssertStaleEntry(result, expectedScoreChange: -1.0m);
        }

        [TestMethod]
        public void Get_DifferentFilePath_ReturnsFalse()
        {
            PutCacheEntry("file1.cs", DefaultBaseline, DefaultCurrent, CreateDelta(-1.0m));

            var result = _cacheService.Get(new DeltaCacheQuery("file2.cs", DefaultBaseline, DefaultCurrent));

            AssertCacheMiss(result);
        }

        private void PutCacheEntry(string path, string baseline, string current, DeltaResponseModel delta)
        {
            _cacheService.Put(new DeltaCacheEntry(path, baseline, current, delta));
        }

        private static DeltaResponseModel CreateDelta(decimal scoreChange)
        {
            return new DeltaResponseModel { ScoreChange = scoreChange };
        }

        private static void AssertCacheHit((bool found, DeltaResponseModel delta) result, decimal expectedScoreChange)
        {
            Assert.IsTrue(result.found);
            Assert.IsNotNull(result.delta);
            Assert.AreEqual(expectedScoreChange, result.delta.ScoreChange);
        }

        private static void AssertCacheMiss((bool found, DeltaResponseModel delta) result)
        {
            Assert.IsFalse(result.found);
            Assert.IsNull(result.delta);
        }

        private static void AssertStaleEntry((bool found, DeltaResponseModel delta) result, decimal expectedScoreChange)
        {
            Assert.IsFalse(result.found, "Stale entry should return found=false");
            Assert.IsNotNull(result.delta, "Stale entry should still return the old delta for reference");
            Assert.AreEqual(expectedScoreChange, result.delta.ScoreChange);
        }

        [TestMethod]
        public void Put_StoresEntryCorrectly()
        {
            // Arrange
            var filePath = "test.cs";
            var baselineContent = "baseline";
            var currentContent = "current";
            var delta = new DeltaResponseModel { ScoreChange = 0.5m };

            var entry = new DeltaCacheEntry(filePath, baselineContent, currentContent, delta);

            // Act
            _cacheService.Put(entry);
            var result = _cacheService.Get(new DeltaCacheQuery(filePath, baselineContent, currentContent));

            // Assert
            Assert.IsTrue(result.Item1);
            Assert.AreEqual(0.5m, result.Item2.ScoreChange);
        }

        [TestMethod]
        public void Put_SameFilePath_UpdatesExistingEntry()
        {
            // Arrange
            var filePath = "test.cs";
            var entry1 = new DeltaCacheEntry(filePath, "old1", "new1", new DeltaResponseModel { ScoreChange = 1.0m });
            var entry2 = new DeltaCacheEntry(filePath, "old2", "new2", new DeltaResponseModel { ScoreChange = 2.0m });

            // Act
            _cacheService.Put(entry1);
            _cacheService.Put(entry2);

            var result1 = _cacheService.Get(new DeltaCacheQuery(filePath, "old1", "new1"));
            var result2 = _cacheService.Get(new DeltaCacheQuery(filePath, "old2", "new2"));

            // Assert
            Assert.IsFalse(result1.Item1); // Old entry should be overwritten
            Assert.IsTrue(result2.Item1); // New entry should be accessible
            Assert.AreEqual(2.0m, result2.Item2.ScoreChange);
        }

        [TestMethod]
        public void GetAll_EmptyCache_ReturnsEmptyDictionary()
        {
            var result = _cacheService.GetAll();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GetAll_WithMultipleEntries_ReturnsAllNonNullDeltas()
        {
            PutMultipleCacheEntries(("file1.cs", 1.0m), ("file2.cs", 2.0m), ("file3.cs", 3.0m));

            var result = _cacheService.GetAll();

            AssertCacheContains(result, expectedCount: 3, expectedFiles: new[] { "file1.cs", "file2.cs", "file3.cs" });
        }

        [TestMethod]
        public void GetAll_ExcludesEntriesWithNullDelta()
        {
            PutCacheEntry("file1.cs", "b1", "c1", CreateDelta(1.0m));
            PutCacheEntry("file2.cs", "b2", "c2", delta: null); // Should be excluded
            PutCacheEntry("file3.cs", "b3", "c3", CreateDelta(3.0m));

            var result = _cacheService.GetAll();

            AssertCacheContains(result, expectedCount: 2, expectedFiles: new[] { "file1.cs", "file3.cs" });
            Assert.IsFalse(result.ContainsKey("file2.cs"));
        }

        private void PutMultipleCacheEntries(params (string file, decimal scoreChange)[] entries)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var (file, scoreChange) = entries[i];
                PutCacheEntry(file, $"b{i + 1}", $"c{i + 1}", CreateDelta(scoreChange));
            }
        }

        private static void AssertCacheContains(System.Collections.Generic.Dictionary<string, DeltaResponseModel> result, int expectedCount, string[] expectedFiles)
        {
            Assert.AreEqual(expectedCount, result.Count);
            foreach (var file in expectedFiles)
            {
                Assert.IsTrue(result.ContainsKey(file), $"Cache should contain {file}");
            }
        }

        [TestMethod]
        public void Clear_RemovesAllEntries()
        {
            // Arrange
            _cacheService.Put(new DeltaCacheEntry("file1.cs", "b1", "c1", new DeltaResponseModel()));
            _cacheService.Put(new DeltaCacheEntry("file2.cs", "b2", "c2", new DeltaResponseModel()));

            // Act
            _cacheService.Clear();
            var result = _cacheService.GetAll();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Invalidate_RemovesSpecificEntry()
        {
            // Arrange
            _cacheService.Put(new DeltaCacheEntry("file1.cs", "b1", "c1", new DeltaResponseModel { ScoreChange = 1.0m }));
            _cacheService.Put(new DeltaCacheEntry("file2.cs", "b2", "c2", new DeltaResponseModel { ScoreChange = 2.0m }));

            // Act
            _cacheService.Invalidate("file1.cs");
            var result = _cacheService.GetAll();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsFalse(result.ContainsKey("file1.cs"));
            Assert.IsTrue(result.ContainsKey("file2.cs"));
        }

        [TestMethod]
        public void UpdateKey_MovesEntryToNewKey()
        {
            // Arrange
            var baseline = "baseline";
            var current = "current";
            var delta = new DeltaResponseModel { ScoreChange = 1.5m };
            _cacheService.Put(new DeltaCacheEntry("old/path.cs", baseline, current, delta));

            // Act
            _cacheService.UpdateKey("old/path.cs", "new/path.cs");

            var oldResult = _cacheService.Get(new DeltaCacheQuery("old/path.cs", baseline, current));
            var newResult = _cacheService.Get(new DeltaCacheQuery("new/path.cs", baseline, current));

            // Assert
            Assert.IsFalse(oldResult.Item1); // Old key should not exist
            Assert.IsTrue(newResult.Item1); // New key should work
            Assert.AreEqual(1.5m, newResult.Item2.ScoreChange);
        }
    }
}
