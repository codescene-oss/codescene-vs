// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
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
    public class ReviewAsync_CacheMiss_DelegatesToInnerReviewerTests
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
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, null, null, _mockLogger.Object, null, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService.Clear();
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "CacheMiss_DelegatesToInnerReviewer.cs";
            var content = "public class CacheMissDelegatesToInnerReviewer { }";
            var expectedResult = new FileReviewModel
            {
                FilePath = path,
                Score = 8.5f,
                RawScore = "raw123",
                FileLevel = new List<CodeSmellModel>(),
                FunctionLevel = new List<CodeSmellModel>(),
            };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _cachingReviewer.ReviewAsync(path, content);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult.Score, result.Score);
            Assert.AreEqual(expectedResult.FilePath, result.FilePath);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
