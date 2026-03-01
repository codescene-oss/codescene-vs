// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class GetOrComputeBaselineRawScoreAsync_BaselineCacheHit_ReturnsCachedScoreTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private Mock<ILogger> _mockLogger = null!;
        private BaselineReviewCacheService _baselineCacheService = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _baselineCacheService = new BaselineReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(
                _mockInnerReviewer.Object,
                null,
                _baselineCacheService,
                null,
                _mockLogger.Object,
                null,
                null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _baselineCacheService.Clear();
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "GetOrComputeBaselineRawScoreAsync_CacheHit.cs";
            var baselineCode = "public class BaselineCode { int x; }";
            var cachedRawScore = "baseline_raw_score_123";

            _baselineCacheService.Put(path, baselineCode, cachedRawScore);

            var result = await _cachingReviewer.GetOrComputeBaselineRawScoreAsync(path, baselineCode);

            Assert.AreEqual(cachedRawScore, result);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
