// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class ReviewAsync_WithoutLogger_DoesNotThrowTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private ReviewCacheService _cacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _cacheService = new ReviewCacheService(new ConcurrentDictionary<string, ReviewCacheItem>());
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, null);
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "WithoutLogger_DoesNotThrow.cs";
            var content = "public class WithoutLoggerDoesNotThrow { }";
            var result = new FileReviewModel { FilePath = path, Score = 8.5f };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var reviewResult = await _cachingReviewer.ReviewAsync(path, content);

            Assert.IsNotNull(reviewResult);
        }
    }
}
