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
    public class ReviewAsync_PathCaseSensitivity_CacheInconsistencyTests
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
        public async Task Test_DifferentCasePaths_SameContent_ShouldOnlyCallInnerReviewerOnce()
        {
            var pathUpperCase = "Foo.cs";
            var pathLowerCase = "foo.cs";
            var content = "public class Foo { }";
            var result = new FileReviewModel { FilePath = pathUpperCase, Score = 8.5f };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(It.IsAny<string>(), content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // First request with uppercase path
            var result1 = await _cachingReviewer.ReviewAsync(pathUpperCase, content);

            // Second request with lowercase path - same content, should ideally hit cache
            var result2 = await _cachingReviewer.ReviewAsync(pathLowerCase, content);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(8.5f, result1.Score);
            Assert.AreEqual(8.5f, result2.Score);

            // This assertion will FAIL if case sensitivity causes cache miss
            _mockInnerReviewer.Verify(
                r => r.ReviewAsync(It.IsAny<string>(), content, false, It.IsAny<CancellationToken>()),
                Times.Once,
                "Same content with different path casing should use cached result, not call inner reviewer twice");
        }
    }
}
