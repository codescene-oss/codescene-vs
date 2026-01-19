using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ReviewCacheServiceTests
    {
        private ReviewCacheService _cacheService;

        [TestInitialize]
        public void Setup()
        {
            _cacheService = new ReviewCacheService();
            _cacheService.Clear(); // Ensure clean state for each test
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public void Get_EmptyCache_ReturnsNull()
        {
            // Arrange
            var query = new ReviewCacheQuery("some content", "test.cs");

            // Act
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Put_ThenGet_WithSameContent_ReturnsCachedResult()
        {
            // Arrange
            var filePath = "test.cs";
            var fileContents = "public class Test { }";
            var response = new FileReviewModel
            {
                FilePath = filePath,
                Score = 8.5f,
                RawScore = "raw123",
                FileLevel = new List<CodeSmellModel>(),
                FunctionLevel = new List<CodeSmellModel>()
            };

            var entry = new ReviewCacheEntry(fileContents, filePath, response);
            var query = new ReviewCacheQuery(fileContents, filePath);

            // Act
            _cacheService.Put(entry);
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(response.Score, result.Score);
            Assert.AreEqual(response.FilePath, result.FilePath);
            Assert.AreEqual(response.RawScore, result.RawScore);
        }

        [TestMethod]
        public void Get_WithDifferentContent_ReturnsNull()
        {
            // Arrange
            var filePath = "test.cs";
            var originalContent = "public class Test { }";
            var modifiedContent = "public class Test { int x; }";
            var response = new FileReviewModel
            {
                FilePath = filePath,
                Score = 8.5f
            };

            var entry = new ReviewCacheEntry(originalContent, filePath, response);
            var query = new ReviewCacheQuery(modifiedContent, filePath);

            // Act
            _cacheService.Put(entry);
            var result = _cacheService.Get(query);

            // Assert
            Assert.IsNull(result, "Cache should miss when content has changed");
        }

        [TestMethod]
        public void Put_SameFilePath_UpdatesCache()
        {
            // Arrange
            var filePath = "test.cs";
            var content1 = "version 1";
            var content2 = "version 2";
            var response1 = new FileReviewModel { FilePath = filePath, Score = 7.0f };
            var response2 = new FileReviewModel { FilePath = filePath, Score = 9.0f };

            // Act
            _cacheService.Put(new ReviewCacheEntry(content1, filePath, response1));
            _cacheService.Put(new ReviewCacheEntry(content2, filePath, response2));
            var result = _cacheService.Get(new ReviewCacheQuery(content2, filePath));

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(9.0f, result.Score);
        }

        [TestMethod]
        public void Get_AfterPutWithDifferentContent_OldContentMisses()
        {
            // Arrange
            var filePath = "test.cs";
            var oldContent = "old content";
            var newContent = "new content";
            var response = new FileReviewModel { FilePath = filePath, Score = 8.0f };

            // Act
            _cacheService.Put(new ReviewCacheEntry(oldContent, filePath, response));
            _cacheService.Put(new ReviewCacheEntry(newContent, filePath, response));
            var result = _cacheService.Get(new ReviewCacheQuery(oldContent, filePath));

            // Assert
            Assert.IsNull(result, "Old content should not match after cache update");
        }

        [TestMethod]
        public void Invalidate_RemovesEntry()
        {
            // Arrange
            var filePath = "test.cs";
            var content = "content";
            var response = new FileReviewModel { FilePath = filePath };

            _cacheService.Put(new ReviewCacheEntry(content, filePath, response));

            // Act
            _cacheService.Invalidate(filePath);
            var result = _cacheService.Get(new ReviewCacheQuery(content, filePath));

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Clear_RemovesAllEntries()
        {
            // Arrange
            _cacheService.Put(new ReviewCacheEntry("content1", "file1.cs", new FileReviewModel()));
            _cacheService.Put(new ReviewCacheEntry("content2", "file2.cs", new FileReviewModel()));

            // Act
            _cacheService.Clear();
            var result1 = _cacheService.Get(new ReviewCacheQuery("content1", "file1.cs"));
            var result2 = _cacheService.Get(new ReviewCacheQuery("content2", "file2.cs"));

            // Assert
            Assert.IsNull(result1);
            Assert.IsNull(result2);
        }

        [TestMethod]
        public void UpdateKey_MovesEntry()
        {
            // Arrange
            var oldPath = "old/path/test.cs";
            var newPath = "new/path/test.cs";
            var content = "content";
            var response = new FileReviewModel { FilePath = oldPath, Score = 7.5f };

            _cacheService.Put(new ReviewCacheEntry(content, oldPath, response));

            // Act
            _cacheService.UpdateKey(oldPath, newPath);
            var oldResult = _cacheService.Get(new ReviewCacheQuery(content, oldPath));
            var newResult = _cacheService.Get(new ReviewCacheQuery(content, newPath));

            // Assert
            Assert.IsNull(oldResult, "Old key should be removed");
            Assert.IsNotNull(newResult, "Entry should be accessible via new key");
            Assert.AreEqual(7.5f, newResult.Score);
        }

        [TestMethod]
        public void MultipleCacheEntries_IndependentlyAccessible()
        {
            // Arrange
            var entry1 = new ReviewCacheEntry("content1", "file1.cs", new FileReviewModel { Score = 1.0f });
            var entry2 = new ReviewCacheEntry("content2", "file2.cs", new FileReviewModel { Score = 2.0f });
            var entry3 = new ReviewCacheEntry("content3", "file3.cs", new FileReviewModel { Score = 3.0f });

            // Act
            _cacheService.Put(entry1);
            _cacheService.Put(entry2);
            _cacheService.Put(entry3);

            var result1 = _cacheService.Get(new ReviewCacheQuery("content1", "file1.cs"));
            var result2 = _cacheService.Get(new ReviewCacheQuery("content2", "file2.cs"));
            var result3 = _cacheService.Get(new ReviewCacheQuery("content3", "file3.cs"));

            // Assert
            Assert.AreEqual(1.0f, result1.Score);
            Assert.AreEqual(2.0f, result2.Score);
            Assert.AreEqual(3.0f, result3.Score);
        }
    }
}
