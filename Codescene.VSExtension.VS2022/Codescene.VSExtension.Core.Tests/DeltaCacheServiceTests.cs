// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class DeltaCacheServiceTests
    {
        private const string DefaultBaseline = "baseline code";
        private const string DefaultCurrent = "current code";

        private DeltaCacheService _cacheService;
        private string _tempFile;
        private string _tempFile1;
        private string _tempFile2;
        private string _tempFile3;

        [TestInitialize]
        public void Setup()
        {
            _cacheService = new DeltaCacheService();
            _cacheService.Clear(); // Ensure clean state for each test

            _tempFile = Path.GetTempFileName();
            _tempFile1 = Path.GetTempFileName();
            _tempFile2 = Path.GetTempFileName();
            _tempFile3 = Path.GetTempFileName();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();

            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }

            if (File.Exists(_tempFile1))
            {
                File.Delete(_tempFile1);
            }

            if (File.Exists(_tempFile2))
            {
                File.Delete(_tempFile2);
            }

            if (File.Exists(_tempFile3))
            {
                File.Delete(_tempFile3);
            }
        }

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
            PutCacheEntry(_tempFile, DefaultBaseline, DefaultCurrent, delta);

            var result = _cacheService.Get(new DeltaCacheQuery(_tempFile, DefaultBaseline, DefaultCurrent));

            AssertCacheHit(result, -0.5m);
        }

        [TestMethod]
        public void Get_CacheHitWithNullDelta_ReturnsTrueAndNull()
        {
            PutCacheEntry(_tempFile, DefaultBaseline, DefaultCurrent, delta: null);

            var result = _cacheService.Get(new DeltaCacheQuery(_tempFile, DefaultBaseline, DefaultCurrent));

            Assert.IsTrue(result.Item1); // Cache hit
            Assert.IsNull(result.Item2); // But delta was null
        }

        [TestMethod]
        public void Get_DifferentBaselineContent_ReturnsStaleEntry()
        {
            PutCacheEntry(_tempFile, "original baseline", DefaultCurrent, CreateDelta(-1.0m));

            var result = _cacheService.Get(new DeltaCacheQuery(_tempFile, "different baseline", DefaultCurrent));

            // Cache returns stale entry (found=false) but still provides old delta for reference
            AssertStaleEntry(result, expectedScoreChange: -1.0m);
        }

        [TestMethod]
        public void Get_DifferentCurrentContent_ReturnsStaleEntry()
        {
            PutCacheEntry(_tempFile, DefaultBaseline, "original current", CreateDelta(-1.0m));

            var result = _cacheService.Get(new DeltaCacheQuery(_tempFile, DefaultBaseline, "different current"));

            // Cache returns stale entry (found=false) but still provides old delta for reference
            AssertStaleEntry(result, expectedScoreChange: -1.0m);
        }

        [TestMethod]
        public void Get_DifferentFilePath_ReturnsFalse()
        {
            PutCacheEntry(_tempFile1, DefaultBaseline, DefaultCurrent, CreateDelta(-1.0m));

            var result = _cacheService.Get(new DeltaCacheQuery(_tempFile2, DefaultBaseline, DefaultCurrent));

            AssertCacheMiss(result);
        }

        [TestMethod]
        public void Put_StoresEntryCorrectly()
        {
            // Arrange
            var baselineContent = "baseline";
            var currentContent = "current";
            var delta = new DeltaResponseModel { ScoreChange = 0.5m };

            var entry = new DeltaCacheEntry(_tempFile, baselineContent, currentContent, delta);

            // Act
            _cacheService.Put(entry);
            var result = _cacheService.Get(new DeltaCacheQuery(_tempFile, baselineContent, currentContent));

            // Assert
            Assert.IsTrue(result.Item1);
            Assert.AreEqual(0.5m, result.Item2.ScoreChange);
        }

        [TestMethod]
        public void Put_SameFilePath_UpdatesExistingEntry()
        {
            // Arrange
            var entry1 = new DeltaCacheEntry(_tempFile, "old1", "new1", new DeltaResponseModel { ScoreChange = 1.0m });
            var entry2 = new DeltaCacheEntry(_tempFile, "old2", "new2", new DeltaResponseModel { ScoreChange = 2.0m });

            // Act
            _cacheService.Put(entry1);
            _cacheService.Put(entry2);

            var result1 = _cacheService.Get(new DeltaCacheQuery(_tempFile, "old1", "new1"));
            var result2 = _cacheService.Get(new DeltaCacheQuery(_tempFile, "old2", "new2"));

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
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void GetAll_WithMultipleEntries_ReturnsAllNonNullDeltas()
        {
            PutMultipleCacheEntries((_tempFile1, 1.0m), (_tempFile2, 2.0m), (_tempFile3, 3.0m));

            var result = _cacheService.GetAll();

            AssertCacheContains(result, expectedCount: 3, expectedFiles: new[] { _tempFile1, _tempFile2, _tempFile3 });
        }

        [TestMethod]
        public void GetAll_ExcludesEntriesWithNullDelta()
        {
            PutCacheEntry(_tempFile1, "b1", "c1", CreateDelta(1.0m));
            PutCacheEntry(_tempFile2, "b2", "c2", delta: null); // Should be excluded
            PutCacheEntry(_tempFile3, "b3", "c3", CreateDelta(3.0m));

            var result = _cacheService.GetAll();

            AssertCacheContains(result, expectedCount: 2, expectedFiles: new[] { _tempFile1, _tempFile3 });
            Assert.IsFalse(result.ContainsKey(_tempFile2));
        }

        [TestMethod]
        public void Clear_RemovesAllEntries()
        {
            // Arrange
            _cacheService.Put(new DeltaCacheEntry(_tempFile1, "b1", "c1", new DeltaResponseModel()));
            _cacheService.Put(new DeltaCacheEntry(_tempFile2, "b2", "c2", new DeltaResponseModel()));

            // Act
            _cacheService.Clear();
            var result = _cacheService.GetAll();

            // Assert
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void Invalidate_RemovesSpecificEntry()
        {
            // Arrange
            _cacheService.Put(new DeltaCacheEntry(_tempFile1, "b1", "c1", new DeltaResponseModel { ScoreChange = 1.0m }));
            _cacheService.Put(new DeltaCacheEntry(_tempFile2, "b2", "c2", new DeltaResponseModel { ScoreChange = 2.0m }));

            // Act
            _cacheService.Invalidate(_tempFile1);
            var result = _cacheService.GetAll();

            // Assert
            Assert.HasCount(1, result);
            Assert.IsFalse(result.ContainsKey(_tempFile1));
            Assert.IsTrue(result.ContainsKey(_tempFile2));
        }

        [TestMethod]
        public void GetDeltaForFile_NullPath_ReturnsNull()
        {
            var result = _cacheService.GetDeltaForFile(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetDeltaForFile_EmptyPath_ReturnsNull()
        {
            var result = _cacheService.GetDeltaForFile(string.Empty);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetDeltaForFile_FileNotInCache_ReturnsNull()
        {
            PutCacheEntry(_tempFile1, "b", "c", CreateDelta(1.0m));

            var result = _cacheService.GetDeltaForFile(_tempFile2);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetDeltaForFile_FileInCache_ReturnsDelta()
        {
            PutCacheEntry(_tempFile, "b", "c", CreateDelta(2.5m));

            var result = _cacheService.GetDeltaForFile(_tempFile);

            Assert.IsNotNull(result);
            Assert.AreEqual(2.5m, result.ScoreChange);
        }

        [TestMethod]
        public void GetDeltaForFile_FileInCacheWithNullDelta_ReturnsNull()
        {
            PutCacheEntry(_tempFile, "b", "c", delta: null);

            var result = _cacheService.GetDeltaForFile(_tempFile);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void UpdateKey_MovesEntryToNewKey()
        {
            // Arrange
            var baseline = "baseline";
            var current = "current";
            var delta = new DeltaResponseModel { ScoreChange = 1.5m };
            _cacheService.Put(new DeltaCacheEntry(_tempFile1, baseline, current, delta));

            // Act
            _cacheService.UpdateKey(_tempFile1, _tempFile2);

            var oldResult = _cacheService.Get(new DeltaCacheQuery(_tempFile1, baseline, current));
            var newResult = _cacheService.Get(new DeltaCacheQuery(_tempFile2, baseline, current));

            // Assert
            Assert.IsFalse(oldResult.Item1); // Old key should not exist
            Assert.IsTrue(newResult.Item1); // New key should work
            Assert.AreEqual(1.5m, newResult.Item2.ScoreChange);
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

        private static void AssertCacheContains(Dictionary<string, DeltaResponseModel> result, int expectedCount, string[] expectedFiles)
        {
            Assert.HasCount(expectedCount, result);
            foreach (var file in expectedFiles)
            {
                Assert.IsTrue(result.ContainsKey(file), $"Cache should contain {file}");
            }
        }

        private void PutCacheEntry(string path, string baseline, string current, DeltaResponseModel delta)
        {
            _cacheService.Put(new DeltaCacheEntry(path, baseline, current, delta));
        }

        private void PutMultipleCacheEntries(params (string file, decimal scoreChange)[] entries)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var (file, scoreChange) = entries[i];
                PutCacheEntry(file, $"b{i + 1}", $"c{i + 1}", CreateDelta(scoreChange));
            }
        }
    }
}
