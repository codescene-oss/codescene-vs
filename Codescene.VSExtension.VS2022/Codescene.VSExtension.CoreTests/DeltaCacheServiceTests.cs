using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.CoreTests
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

        #region Get Tests

        [TestMethod]
        public void Get_EmptyCache_ReturnsFalseAndNull()
        {
            // Arrange
            var query = new DeltaCacheQuery("test.cs", "baseline content", "current content");

            // Act
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsFalse(result.Item1);
            Assert.IsNull(result.Item2);
        }

        [TestMethod]
        public void Get_CacheHitWithMatchingHashes_ReturnsTrueAndDelta()
        {
            // Arrange
            var filePath = "test.cs";
            var baselineContent = "old code";
            var currentContent = "new code";
            var delta = new DeltaResponseModel
            {
                OldScore = 8.0m,
                NewScore = 7.5m,
                ScoreChange = -0.5m
            };

            var entry = new DeltaCacheEntry(filePath, baselineContent, currentContent, delta);
            _cacheService.Put(entry);

            var query = new DeltaCacheQuery(filePath, baselineContent, currentContent);

            // Act
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsTrue(result.Item1);
            Assert.IsNotNull(result.Item2);
            Assert.AreEqual(-0.5m, result.Item2.ScoreChange);
        }

        [TestMethod]
        public void Get_CacheHitWithNullDelta_ReturnsTrueAndNull()
        {
            // Arrange
            var filePath = "test.cs";
            var baselineContent = "old code";
            var currentContent = "new code";

            var entry = new DeltaCacheEntry(filePath, baselineContent, currentContent, null);
            _cacheService.Put(entry);

            var query = new DeltaCacheQuery(filePath, baselineContent, currentContent);

            // Act
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsTrue(result.Item1); // Cache hit
            Assert.IsNull(result.Item2); // But delta was null
        }

        [TestMethod]
        public void Get_DifferentBaselineContent_ReturnsFalse()
        {
            // Arrange
            var filePath = "test.cs";
            var originalBaseline = "original baseline";
            var currentContent = "current code";
            var delta = new DeltaResponseModel { ScoreChange = -1.0m };

            var entry = new DeltaCacheEntry(filePath, originalBaseline, currentContent, delta);
            _cacheService.Put(entry);

            var query = new DeltaCacheQuery(filePath, "different baseline", currentContent);

            // Act
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsFalse(result.Item1); // Cache miss due to different baseline hash
        }

        [TestMethod]
        public void Get_DifferentCurrentContent_ReturnsFalse()
        {
            // Arrange
            var filePath = "test.cs";
            var baselineContent = "baseline code";
            var originalCurrent = "original current";
            var delta = new DeltaResponseModel { ScoreChange = -1.0m };

            var entry = new DeltaCacheEntry(filePath, baselineContent, originalCurrent, delta);
            _cacheService.Put(entry);

            var query = new DeltaCacheQuery(filePath, baselineContent, "different current");

            // Act
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsFalse(result.Item1); // Cache miss due to different current hash
        }

        [TestMethod]
        public void Get_DifferentFilePath_ReturnsFalse()
        {
            // Arrange
            var baselineContent = "baseline";
            var currentContent = "current";
            var delta = new DeltaResponseModel { ScoreChange = -1.0m };

            var entry = new DeltaCacheEntry("file1.cs", baselineContent, currentContent, delta);
            _cacheService.Put(entry);

            var query = new DeltaCacheQuery("file2.cs", baselineContent, currentContent);

            // Act
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsFalse(result.Item1);
            Assert.IsNull(result.Item2);
        }

        #endregion

        #region Put Tests

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

        #endregion

        #region GetAll Tests

        [TestMethod]
        public void GetAll_EmptyCache_ReturnsEmptyDictionary()
        {
            // Act
            var result = _cacheService.GetAll();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GetAll_WithMultipleEntries_ReturnsAllNonNullDeltas()
        {
            // Arrange
            var entry1 = new DeltaCacheEntry("file1.cs", "b1", "c1", new DeltaResponseModel { ScoreChange = 1.0m });
            var entry2 = new DeltaCacheEntry("file2.cs", "b2", "c2", new DeltaResponseModel { ScoreChange = 2.0m });
            var entry3 = new DeltaCacheEntry("file3.cs", "b3", "c3", new DeltaResponseModel { ScoreChange = 3.0m });

            _cacheService.Put(entry1);
            _cacheService.Put(entry2);
            _cacheService.Put(entry3);

            // Act
            var result = _cacheService.GetAll();

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.ContainsKey("file1.cs"));
            Assert.IsTrue(result.ContainsKey("file2.cs"));
            Assert.IsTrue(result.ContainsKey("file3.cs"));
        }

        [TestMethod]
        public void GetAll_ExcludesEntriesWithNullDelta()
        {
            // Arrange
            var entry1 = new DeltaCacheEntry("file1.cs", "b1", "c1", new DeltaResponseModel { ScoreChange = 1.0m });
            var entry2 = new DeltaCacheEntry("file2.cs", "b2", "c2", null); // null delta
            var entry3 = new DeltaCacheEntry("file3.cs", "b3", "c3", new DeltaResponseModel { ScoreChange = 3.0m });

            _cacheService.Put(entry1);
            _cacheService.Put(entry2);
            _cacheService.Put(entry3);

            // Act
            var result = _cacheService.GetAll();

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.ContainsKey("file1.cs"));
            Assert.IsFalse(result.ContainsKey("file2.cs")); // Should be excluded
            Assert.IsTrue(result.ContainsKey("file3.cs"));
        }

        #endregion

        #region Inherited CacheService Methods Tests

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

        #endregion
    }
}
