// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    [TestClass]
    public class ReviewAsync_WithDefaultCache_CreatesNewCacheInstanceTests
    {
        private Mock<ICodeReviewer> _mockInnerReviewer = null!;
        private CachingCodeReviewer _cachingReviewer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object);
        }

        [TestMethod]
        public async Task Test()
        {
            var path = "WithDefaultCache_CreatesNewInstance.cs";
            var content = "public class WithDefaultCacheCreatesNewInstance { }";
            var result = new FileReviewModel { FilePath = path, Score = 8.5f };

            _mockInnerReviewer
                .Setup(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var firstResult = await _cachingReviewer.ReviewAsync(path, content);
            var secondResult = await _cachingReviewer.ReviewAsync(path, content);

            Assert.IsNotNull(firstResult);
            Assert.IsNotNull(secondResult);
            _mockInnerReviewer.Verify(r => r.ReviewAsync(path, content, false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
