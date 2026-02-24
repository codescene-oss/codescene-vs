// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class ReviewAsync_CacheMiss_CachesResultTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private ReviewCacheService _cacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _cacheService = new ReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, _mockLogger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "CacheMiss_CachesResult.cs";
            var content = "public class CacheMissCachesResult { }";
            var expectedResult = new FileReviewModel
            {
                FilePath = path,
                Score = 8.5f,
            };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            await _cachingReviewer.ReviewAsync(path, content);

            var cachedResult = _cacheService.Get(new Models.Cache.Review.ReviewCacheQuery(content, path.ToLowerInvariant()));
            Assert.IsNotNull(cachedResult);
            Assert.AreEqual(expectedResult.Score, cachedResult.Score);
        }
    }
}
