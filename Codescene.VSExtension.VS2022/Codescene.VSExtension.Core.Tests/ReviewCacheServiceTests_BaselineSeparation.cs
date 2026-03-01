// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ReviewCacheServiceTests_BaselineSeparation
    {
        private ReviewCacheService _cacheService = null!;

        [TestInitialize]
        public void Setup()
        {
            _cacheService = new ReviewCacheService();
            _cacheService.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public void BaselineAndNonBaseline_CachedSeparately()
        {
            var filePath = "test.cs";
            var fileContents = "public class Test { }";
            var nonBaselineResponse = new FileReviewModel
            {
                FilePath = filePath,
                Score = 8.0f,
                RawScore = "raw-non-baseline",
            };
            var baselineResponse = new FileReviewModel
            {
                FilePath = filePath,
                Score = 7.0f,
                RawScore = "raw-baseline",
            };

            var nonBaselineEntry = new ReviewCacheEntry(fileContents, filePath, nonBaselineResponse, isBaseline: false);
            var baselineEntry = new ReviewCacheEntry(fileContents, filePath, baselineResponse, isBaseline: true);

            _cacheService.Put(nonBaselineEntry);
            _cacheService.Put(baselineEntry);

            var nonBaselineQuery = new ReviewCacheQuery(fileContents, filePath, isBaseline: false);
            var baselineQuery = new ReviewCacheQuery(fileContents, filePath, isBaseline: true);

            var nonBaselineResult = _cacheService.Get(nonBaselineQuery);
            var baselineResult = _cacheService.Get(baselineQuery);

            Assert.IsNotNull(nonBaselineResult);
            Assert.IsNotNull(baselineResult);
            Assert.AreEqual(8.0f, nonBaselineResult.Score);
            Assert.AreEqual(7.0f, baselineResult.Score);
            Assert.AreEqual("raw-non-baseline", nonBaselineResult.RawScore);
            Assert.AreEqual("raw-baseline", baselineResult.RawScore);
        }

        [DataRow(false, true, DisplayName = "BaselineQuery_DoesNotMatchNonBaselineCache")]
        [DataRow(true, false, DisplayName = "NonBaselineQuery_DoesNotMatchBaselineCache")]
        [TestMethod]
        public void Query_DoesNotMatchDifferentBaselineMode(bool cacheIsBaseline, bool queryIsBaseline)
        {
            var filePath = "test.cs";
            var fileContents = "public class Test { }";
            var response = new FileReviewModel
            {
                FilePath = filePath,
                Score = 8.0f,
            };

            var entry = new ReviewCacheEntry(fileContents, filePath, response, isBaseline: cacheIsBaseline);
            _cacheService.Put(entry);

            var query = new ReviewCacheQuery(fileContents, filePath, isBaseline: queryIsBaseline);
            var result = _cacheService.Get(query);

            Assert.IsNull(result, $"Query with isBaseline={queryIsBaseline} should not match cache entry with isBaseline={cacheIsBaseline}");
        }

        [TestMethod]
        public void Invalidate_RemovesBothBaselineAndNonBaseline()
        {
            var filePath = "test.cs";
            var fileContents = "public class Test { }";
            var nonBaselineResponse = new FileReviewModel { FilePath = filePath, Score = 8.0f };
            var baselineResponse = new FileReviewModel { FilePath = filePath, Score = 7.0f };

            _cacheService.Put(new ReviewCacheEntry(fileContents, filePath, nonBaselineResponse, isBaseline: false));
            _cacheService.Put(new ReviewCacheEntry(fileContents, filePath, baselineResponse, isBaseline: true));

            _cacheService.Invalidate(filePath);

            var nonBaselineResult = _cacheService.Get(new ReviewCacheQuery(fileContents, filePath, isBaseline: false));
            var baselineResult = _cacheService.Get(new ReviewCacheQuery(fileContents, filePath, isBaseline: true));

            Assert.IsNull(nonBaselineResult, "Non-baseline entry should be invalidated");
            Assert.IsNull(baselineResult, "Baseline entry should be invalidated");
        }

        [TestMethod]
        public void UpdateKey_MovesBothBaselineAndNonBaseline()
        {
            var oldPath = "old/test.cs";
            var newPath = "new/test.cs";
            var fileContents = "public class Test { }";
            var nonBaselineResponse = new FileReviewModel { FilePath = oldPath, Score = 8.0f };
            var baselineResponse = new FileReviewModel { FilePath = oldPath, Score = 7.0f };

            _cacheService.Put(new ReviewCacheEntry(fileContents, oldPath, nonBaselineResponse, isBaseline: false));
            _cacheService.Put(new ReviewCacheEntry(fileContents, oldPath, baselineResponse, isBaseline: true));

            _cacheService.UpdateKey(oldPath, newPath);

            var oldNonBaselineResult = _cacheService.Get(new ReviewCacheQuery(fileContents, oldPath, isBaseline: false));
            var oldBaselineResult = _cacheService.Get(new ReviewCacheQuery(fileContents, oldPath, isBaseline: true));
            var newNonBaselineResult = _cacheService.Get(new ReviewCacheQuery(fileContents, newPath, isBaseline: false));
            var newBaselineResult = _cacheService.Get(new ReviewCacheQuery(fileContents, newPath, isBaseline: true));

            Assert.IsNull(oldNonBaselineResult, "Old non-baseline entry should be removed");
            Assert.IsNull(oldBaselineResult, "Old baseline entry should be removed");
            Assert.IsNotNull(newNonBaselineResult, "Non-baseline entry should be moved to new key");
            Assert.IsNotNull(newBaselineResult, "Baseline entry should be moved to new key");
            Assert.AreEqual(8.0f, newNonBaselineResult.Score);
            Assert.AreEqual(7.0f, newBaselineResult.Score);
        }
    }
}
